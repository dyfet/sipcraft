// Copyright (C) 2025 Tycho Softworks. Licensed under CC BY-NC-ND 4.0.

using System.Net;
using System.Reflection;
using SIPSorcery.SIP;
using Tychosoft.Extensions;
using Microsoft.Extensions.Configuration;

namespace sipcraft {
    // Extension events...
    public static class Local {
        private static readonly string server_agent = "SIPCraft/" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown");
        private static SIPTransport transport = null!;

        public static async Task OnRequests(SIPEndPoint local, SIPEndPoint remote, SIPRequest request) {
            Logger.Trace($"sip request {local} {remote}: {request.Method}");
            try {
                SIPResponse response;
                if (request.Method == SIPMethodsEnum.REGISTER) {
                    if (!request.Header.HasAuthenticationHeader) {
                        response = Unauthorized(request);
                    }
                    else {
                       response = Registry.Refresh(local, remote, request);
                    }
                }
                else {
                    response = SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.MethodNotAllowed, "Method Not Recognized");
                }
                response.Header.Server = server_agent;
                await transport.SendResponseAsync(response);
            }
            catch(Exception e) {
                Logger.Error($"failed: {e.Message}");
            }
        }

        public static void Startup(IConfigurationRoot config) {
            var keys = config.GetSection("server");
            if(!int.TryParse(keys["port"], out int port)) {
                port = 5060;
            }

            if(string.IsNullOrEmpty(keys["bind"]) || !IPAddress.TryParse(keys["bind"], out IPAddress? bind)) {
                bind = IPAddress.Any;
            }

            Logger.Debug($"binding {bind}:{port}");
            transport = new SIPTransport();
            transport.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(bind, port)));
            transport.AddSIPChannel(new SIPTCPChannel(new IPEndPoint(bind, port)));
            transport.SIPTransportRequestReceived += async (local, remote, request) =>
                await Local.OnRequests(local, remote, request);
        }

        public static void Reload(IConfigurationRoot config) {
        }

        public static void Shutdown() {
            transport.Shutdown();
        }

        private static SIPResponse Unauthorized(SIPRequest request) {
            var nonce = Guid.NewGuid().ToString();
            var auth = new SIPAuthenticationHeader(SIPAuthorisationHeadersEnum.WWWAuthenticate, Registry.Realm(), nonce);
            var response = SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Unauthorised, null);
            response.Header.AuthenticationHeaders.Add(auth);
            return response;
        }
    }
} // end namespace

