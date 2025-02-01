// Copyright (C) 2025 Tycho Softworks. Licensed under CC BY-NC-ND 4.0.

using System;
using System.Linq;
using Npgsql;
using Tychosoft.Extensions;
using Microsoft.Extensions.Configuration;

namespace sipcraft {
    public static class Database {
        private static readonly Thread listen = new(Listen);
        private static readonly TaskQueue tq = new();
        private static string? dsn = "none";
        private static bool listening = false;
        private static ulong series = 0;
        private static SemaphoreCount limiter = null!;
        private static NpgsqlConnection listener = null!;

        public static void Startup(ServerConfig config) {
            tq.Startup();
            limiter = new SemaphoreCount((int)config.connections);
            dsn = config.dsn;
            if (dsn == "none") {
                return;
            }

            try {
                listener = new NpgsqlConnection(dsn);
                listener.Open();
            }
            catch (Exception ex) {
                Logger.Fatal(-2, $"Database failed; {ex.Message}");
            }

            listener.Notification += OnNotification;
            using (var sql = new NpgsqlCommand("LISTEN sipcraft;", listener)) {
                sql.ExecuteNonQuery();
            }

            Logger.Info("startup Database");
            listening = true;
            listen.Start();
            Resync();
        }

        public static void Reload(ServerConfig config) {
            if(dsn != "none") {
                limiter.Reset((int)config.connections);
                Resync();
            }
        }

        public static void Shutdown() {
            tq.Shutdown();
            if (listening) {
                listening = false;
                dsn = "none";
                listener.Close();
                listen.Join();
                Logger.Info("shutdown Database");
            }
        }

        public static bool Notify(string message) {
            if(dsn != "none") {
                return tq.Dispatch(static args => {
                    string message = (string)args[0];
                    using var sql = new NpgsqlCommand("NOTIFY backend, @payload;", listener);
                    sql.Parameters.AddWithValue("payload", message);
                    sql.ExecuteNonQuery();
                }, message);
            }
            return false;
        }

        private static void Resync() {
            if (dsn == "none") {
                return;
            }

            try {
                limiter.Wait();
            }
            catch {
                return;
            }

            using var db = new NpgsqlConnection(dsn);
            try {
                db.Open();
                string query = "SELECT ext.id, user.name, ext.display, ext.type, ext.user, auth.secret FROM ext JOIN auth ON ext.user = auth.user";
                using var sql = new NpgsqlCommand(query, db);
                using var reader = sql.ExecuteReader();
                ++series;
                while (reader.Read()) {
                    if( uint.TryParse(reader.GetString(0), out uint id)) {
                        var ext = new Extension(id) {
                            Name = reader.GetString(1),
                            Display = reader.GetString(2),
                            Type = GetType(reader.GetString(3)),
                            Auth = reader.GetString(4),
                            Secret = reader.GetString(5),
                            Series = series
                        };
                        Registry.Update(ext);
                    }
                }
                Registry.Sync(series);
            }
            catch(Exception ex) {
                Logger.Error($"Query: {ex.Message}");
            }
            finally {
                limiter.Post();
            }
        }

        private static void Listen() {
            while (listening) {
                listener.Wait();
                using var sql = new NpgsqlCommand("SELECT 1;", listener);
                sql.ExecuteNonQuery();
            }
        }

        private static void OnNotification(object sender, NpgsqlNotificationEventArgs e) {
            Logger.Debug($"Notification {e.Payload}");
        }

        private static ExtType GetType(string str) {
            if(Enum.TryParse<ExtType>(str, out ExtType type)) {
                return type;
            }
            return ExtType.USER;
        }
    }
} // end namespace

