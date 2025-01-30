// Copyright (C) 2025 Tycho Softworks. Licensed under CC BY-NC-ND 4.0.

using System.Runtime.Loader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Primitives;
using System.Diagnostics;
using System.Runtime;
using Tychosoft.Extensions;

#if UNIX
using System.Runtime.InteropServices;
#else
using System.ServiceProcess;
using System.Management;
#endif

namespace sipcraft {
    class Program {
        private static bool running = true;
        private static int exitCode = 0;
        private static readonly ManualResetEventSlim exitEvent = new(false);

        public static bool IsRunning() {
            return running;
        }

        static void Start(string[] args) {
#if UNIX
            string sysConfDir = "/etc";
            string sysPrefix = "/var/lib/sipcraft";
            string logPath = "/var/log/sipcraft.log";
            bool reload = false;
#else
            string sysConfDir = @"C:\ProgramData\sipcraft";
            string sysPrefix = sysConfDir;
            string logPath = @"C:\ProgramData\sipcraft\sipcraft.log";
            bool reload = true;
#endif

            var app = new CommandLineApplication();
            var helpOption = app.Option("-h|--help|-?", "Show help info", CommandOptionType.NoValue);
            var prefixOption = app.Option("-p|--prefix <Directory>", "Change working directory", CommandOptionType.SingleValue);
            var verboseOption = app.Option("-v|--verbose", "Show verbose traces", CommandOptionType.NoValue);

            app.OnExecute(() => {
                if(helpOption.HasValue()) {
                    app.ShowHelp();
                    Environment.Exit(0);
                }

                if(verboseOption.HasValue()) {
                    Logger.SetVerbose();
                }

                if(prefixOption.HasValue()) {
                    var directory = prefixOption.Value();
                    if(Directory.Exists(directory)) {
                        Directory.SetCurrentDirectory(directory);
                        Console.WriteLine($"Changed working directory to: {directory}");
                    }
                    else {
                        Console.WriteLine($"Directory not found: {directory}");
                        return 1; // Return error code
                    }
                }
                return 0;
            });

            try {
                app.Execute(args);
                if(Directory.Exists(sysPrefix)) {
                    Directory.SetCurrentDirectory(sysPrefix);
                }
                else if(!IsService()) {
                    sysPrefix = Directory.GetCurrentDirectory();
                }
                else {
                    Console.WriteLine($"Prefix missing: {sysPrefix}");
                    Environment.Exit(3);
                }
            }
            catch(Exception e) {
                Console.WriteLine($"Arguments Error: {e.Message}");
                Environment.Exit(2);
            }

            if(!File.Exists(sysConfDir + "/sipcraft.conf")) {
                sysConfDir = sysPrefix;
            }

            if(!IsService()) {
                logPath = "./sipcraft.log";
                File.Delete(logPath);
            }
            Logger.Startup("sipcraft", logPath);

            try {
                GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
                var proc = Process.GetCurrentProcess();
                proc.PriorityClass = ProcessPriorityClass.High;
            }
            catch(Exception ex) {
                Logger.Info($"priority: {ex.Message}");
            }

            try {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(sysConfDir)
                    .AddIniFile("sipcraft.conf", optional: true, reloadOnChange: reload);
                Logger.Info($"config path: {sysConfDir}");
                var config = builder.Build();
#if WINDOWS
                ChangeToken.OnChange(() => config.GetReloadToken(), () => {
                    Reload(config);
                });
#endif

                Registry.Startup(config);
                Database.Startup(config);
                Local.Startup(config);
                Logger.Info("server started");
                Console.CancelKeyPress += (sender, e) => {
                    e.Cancel = true;
                    exitEvent.Set();
                };

                AssemblyLoadContext.Default.Unloading += ctx => {
                    exitEvent.Set();
                };

#if UNIX
                [DllImport("libc")] static extern void signal(int signal, SignalHandler handler);
                SignalHandler sighup = signalNumber => {
                    ((IConfigurationRoot)config).Reload();
                    Reload(config);
                };

                SignalHandler sigexit = signalNumber => {
                    Logger.Info($"exit signal {signalNumber}");
                    exitEvent.Set();
                };

                signal(1, sighup);
                signal(2, sigexit);
                signal(3, sigexit);
                signal(9, sigexit);
                signal(15, sigexit);
#endif
                GC.Collect();
                exitEvent.Wait();
                Exit();
            }
            catch(Exception e) {
                Logger.Fatal(1, $"server failed: {e.Message}");
            }
        }

        private static void Exit() {
            running = false;
            Local.Shutdown();
            Registry.Shutdown();
            Database.Shutdown();
            Logger.Info($"server exiting; reason={exitCode}");
            Logger.Shutdown();
            Environment.Exit(exitCode);
        }

        private static void Reload(IConfigurationRoot config) {
            Logger.Info("reload server");
            Local.Reload(config);
            Registry.Reload(config);
            Database.Reload(config);
        }

#if WINDOWS
        public class SIPService : ServiceBase {
            protected override void OnStart(string[] args) {
                Start(args);
            }

            protected override void OnStop() {
                exitEvent.Set();
            }
        }

        public static bool IsService() {
            using var process = Process.GetCurrentProcess();
            var parentProcess = GetParentProcess(process.Id);
            return parentProcess != null && parentProcess.ProcessName == "services";
        }

        private static Process? GetParentProcess(int id) {
            try {
                var query = $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {id}";
                using var search = new System.Management.ManagementObjectSearcher("root\\CIMV2", query);
                return search.Get().OfType<System.Management.ManagementObject>().Select(p => Process.GetProcessById((int)(uint)p["ParentProcessId"])).FirstOrDefault();
                }
            catch {
                return null;
            }
        }

        static void Main(string[] args) {
            if(IsService()) {
                var services = new ServiceBase[] { new SIPService() };
                ServiceBase.Run(services);
            }
            else {
                Start(args);
            }
        }
#else
        private delegate void SignalHandler(int signalNumber);

        public static bool IsService() {
            [DllImport("libc")] static extern int getppid();
            [DllImport("libc")] static extern int getpid();
            [DllImport("libc")] static extern int getuid();

            return getppid() == 1 || getpid() == 1 || getuid() == 0;
        }

        static void Main(string[] args) {
            Start(args);
        }
#endif
    }
} // end namespace

