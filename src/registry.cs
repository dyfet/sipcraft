// Copyright (C) 2025 Tycho Softworks. Licensed under CC BY-NC-ND 4.0.

using System;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using SIPSorcery.SIP;
using Tychosoft.Extensions;

namespace sipcraft {
    using Extensions = ConcurrentDictionary<uint, Extension>;

    public enum ExtType {
        USER,
        GROUP,
        ADMIN
    }

    public class Endpoint {
        public SIPEndPoint? Remote { get; set; } = null;
        public SIPEndPoint? Local { get; set; } = null;
        public DateTime Expires { get; set; } = DateTime.MinValue;
    }

    public class Extension {
        public uint Id { get; init; }
        public string Name { get; set; } = string.Empty;
        public string Auth { get; set; } = string.Empty;
        public ExtType Type { get; set; } = ExtType.USER;
        public string Display { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;

        // some internal controls...
        public bool Preset { get; set; } = false;
        public ulong Series { get; set; } = 0;

        // everything here and below must be lock managed...
        private Object sync { get; set; } = new();
        private DateTime expires { get; set; } = DateTime.MinValue;
        private List<Endpoint> endpoints { get; set; } = new();

        public Extension() {}   // default for dapper...

        public Extension(uint id) {
            Id = id;
            Name = "User " + id;
            Auth = "" + id;
            Display = "Ext " + id;
        }

        public void Refresh(Endpoint endpoint) {
            lock (sync) {
                if (endpoint.Expires > expires)
                    expires = endpoint.Expires;
                foreach (Endpoint ep in endpoints) {
                    if (ep.Remote == endpoint.Remote) {
                        ep.Expires = endpoint.Expires;
                        return;
                    }
                }
                endpoints.Add(endpoint);
            }
        }

        public bool IsExpired() {
            lock (sync) {
                return expires < DateTime.Now;
            }
        }

        public void Expire() {
            List<Endpoint> list = new();
            lock (sync) {
                foreach (Endpoint ep in endpoints) {
                    if (ep.Expires <= DateTime.Now) continue;
                    list.Add(ep);
                    if (ep.Expires > expires)
                        expires = ep.Expires;
                }
                endpoints = list;
            }
        }

        public void Retain(Extension older) {
            lock (older.sync) {
                sync = older.sync;
                endpoints = older.endpoints;
            }
        }
    }

    public static class Registry {
        private static readonly Extensions extensions = new();
        private static string realm = Environment.MachineName;

        public static string Realm() {
            return realm;
        }

        public static Extension? Get(uint id) {
            if (extensions.TryGetValue(id, out Extension? ext)) return ext;
            return null;
        }

        public static void Sync(ulong series) {
            List<uint> list = new();
            foreach (var kvp in extensions) {
                if(kvp.Value.Preset == false && kvp.Value.Series < series)
                    list.Add(kvp.Key);
            }
            foreach (var key in list) {
                extensions.TryRemove(key, out _);
            }
        }

        public static void Each(Action<KeyValuePair<uint, Extension>> action) {
            foreach (var kvp in extensions) {
                action(kvp);
            }
        }

        public static List<Extension> Select(Func<KeyValuePair<uint, Extension>, Extension> func) {
            var results = new List<Extension>();
            foreach (var kvp in extensions) {
                var ext = func(kvp);
                if (ext != null)
                    results.Add(ext);
            }
            results.Sort((x, y) => x.Id.CompareTo(y.Id));
            return results;
        }

        public static bool Exists(uint id) {
            return extensions.ContainsKey(id);
        }

        public static bool Update(Extension ext) {
            if (String.IsNullOrEmpty(ext.Display))
                ext.Display = ext.Name;

            if (String.IsNullOrEmpty(ext.Display))
                ext.Display = "Ext " + ext.Id;

            var old = Get(ext.Id);
            if (old != null) {
                if (old.Preset == true && ext.Preset == false) return false;
                ext.Retain(old);
            }
            return extensions.AddOrUpdate(ext.Id, ext, (key, old) => ext) != null;
        }

        public static bool Remove(uint id) {
            return extensions.TryRemove(id, out _);
        }

        public static void Startup(IConfigurationRoot config) {
            Logger.Info("startup Registry");
            var keys = config.GetSection("server");
            realm = keys["realm"] ?? Environment.MachineName;
            Load(config);
        }

        public static void Shutdown() {
            Logger.Info("shutdown Registry");
        }

        public static void Reload(IConfigurationRoot config) {
            var list = new List<uint>();
            foreach (var ext in extensions.Values) {
                if (ext.Preset && (config.GetSection(ext.Id.ToString()) == null)) {
                    // remove preset extensions not in config}
                    list.Add(ext.Id);
                }
            }
            foreach (var id in list) {
                Remove(id);
            }
            Load(config);
        }

        public static SIPResponse Refresh(SIPEndPoint local, SIPEndPoint remote, SIPRequest request) {
            var contact = request.Header.Contact.FirstOrDefault();
            if (contact == null) return SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.BadRequest, "Missing Contact Header");
            var uri = contact.ContactURI;
            var user = uri.User;
            Extension? ext = null;
            if (uint.TryParse(user, out uint id))
                ext = Get(id);
            if (ext == null) return SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.NotFound, "User mpt found");
            var auth = request.Header.AuthenticationHeaders[0];
            if (auth.SIPDigest.Username != ext.Auth) return SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Unauthorised, "User not valid");
            var nonce = auth.SIPDigest.Nonce;
            var digest = auth.SIPDigest.Response;
            var ha2 = ComputeDigest($"{request.Method}.{request.URI}");
            var expects = ComputeDigest("{ext.Secret}:{providedNonce}:{ha2}");
            if (expects != digest) return SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Unauthorised, null);
            var endpoint = new Endpoint {
                Remote = remote,
                Local = local,
                Expires = DateTime.Now.AddSeconds(120)
            };
            ext.Refresh(endpoint);
            return SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Ok, null);
        }

        public static string ComputeDigest(string input) {
            using var md5 = MD5.Create();
            var digest = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(digest).Replace("-", "").ToLower();
        }

        private static void Load(IConfigurationRoot config) {
            foreach (var section in config.GetChildren()) {
                if (uint.TryParse(section.Key, out uint id)) {
                    var ext = new Extension(id);
                    ext.Name = section["name"] ?? ext.Name;
                    ext.Type = Enum.Parse<ExtType>(section["type"] ?? ext.Type.ToString());
                    ext.Display = section["display"] ?? ext.Display;
                    ext.Secret = section["secret"] ?? ext.Secret;
                    ext.Preset = true;
                    Update(ext);
                }
            }
        }
    }
} // end namespace

