using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows.Forms;

namespace HereTTP
{
    static class Program
    {
        static Dictionary<string, string> COMMAND_ALIASES = new Dictionary<string, string>()
        {
            {"--port", "port" },
            {"\\P", "port" },
            {"-p", "port" },
            {"--browser", "browser" },
            {"\\B", "browser" },
            {"-b", "browser" }
        };

        static Dictionary<string, string> DEFAULT_ARGUMENTS = new Dictionary<string, string>()
        {
            {"port", "80" },
            {"browser", "explorer" },
            {"path", Environment.CurrentDirectory }
        };

        static void SetDefaults(Dictionary<string, string> args)
        {
            foreach (var pair in DEFAULT_ARGUMENTS)
            {
                args[pair.Key] = pair.Value;
            }
        }

        static string SettingsFile
        {
            get
            {
                return Path.Combine(Environment.CurrentDirectory, "settings.ini");
            }
        }

        static void ReadINI(Dictionary<string, string> args)
        {
            if (File.Exists(SettingsFile))
            {
                var lines = File.ReadAllLines(SettingsFile);
                for (var i = 0; i < lines.Length; ++i)
                {
                    if (lines[i].Contains("="))
                    {
                        var parts = lines[i].Split('=');
                        args[parts[0]] = parts[1];
                    }
                }
            }
        }

        static void WriteINI(Dictionary<string, string> args)
        {
            var output = new List<string>();
            foreach (var key in args.Keys)
            {
                if (key != "path" && !(DEFAULT_ARGUMENTS.ContainsKey(key) && DEFAULT_ARGUMENTS[key] == args[key]))
                {
                    output.Add(string.Format("{0}={1}", key, args[key]));
                }
            }
            if (output.Count > 0)
            {
                File.WriteAllLines(SettingsFile, output.ToArray());
            }
        }

        static void ReadCommandLine(string[] args, Dictionary<string, string> arguments)
        {
            for (int i = 0; i < args.Length - 1; i += 2)
            {
                if (!COMMAND_ALIASES.ContainsKey(args[i]))
                {
                    Console.Error.WriteLine("Unknown command switch: {0}.", args[i]);
                    return;
                }
                else {
                    arguments[COMMAND_ALIASES[args[i]]] = args[i + 1];
                }
            }
            if (args.Length % 2 == 1)
            {
                arguments["path"] = args[args.Length - 1];
            }
        }

        static void StartProc(string exe, bool withAdmin, params string[] args)
        {
            var startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.WorkingDirectory = Environment.CurrentDirectory;
            startInfo.FileName = exe;
            if (withAdmin)
            {
                startInfo.Verb = "runas";
            }
            if (args != null)
            {
                startInfo.Arguments = String.Join(" ", args);
            }
            Process p = Process.Start(startInfo);
        }

        static bool Elevated(string[] args)
        {
            Console.Write("Checking admin privileges... ");
            var pricipal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            if (pricipal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                Console.WriteLine("already admin.");
                return true;
            }
            else
            {
                Console.WriteLine("need to elevate.");
                StartProc(Application.ExecutablePath, true, args);
                return false;
            }
        }

        static SimpleHTTPServer StartServer(string path, string browser, int port)
        {
            var server = new SimpleHTTPServer(path, port);
            server.Ready += new EventHandler((o, e) =>
            {
                StartBrowser(browser, port);
            });
            return server;
        }

        private static void StartBrowser(string browser, int port)
        {
            Console.WriteLine("Running. Hit X or CTRL+C to exit. Hit B to select a new browser.");
            var builder = new UriBuilder();
            builder.Host = "localhost";
            if (port != 80)
            {
                builder.Port = port;
            }
            builder.Scheme = "http:";
            var url = builder.ToString();
            Console.WriteLine("Starting browser '{0}' at '{1}'", browser, url);
            if (browser.Contains("chrome"))
            {
                StartProc(browser, false, "--kiosk", url);
            }
            else
            {
                StartProc(browser, false, url);
            }
        }

        static OpenFileDialog openFile = null;
        static void ShowFindBrowserDialog(Dictionary<string, string> args)
        {
            if (openFile == null)
            {
                openFile = new OpenFileDialog
                {
                    AddExtension = true,
                    AutoUpgradeEnabled = true,
                    CheckFileExists = true,
                    CheckPathExists = true,
                    DefaultExt = ".exe",
                    DereferenceLinks = true,
                    Filter = "Executable files|*.exe|Command files|*.cmd|Batch scripts|*.bat|All files|*.*",
                    FilterIndex = 0,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Multiselect = false,
                    Title = "Specify custom browser executable path."
                };
            }
            var result = openFile.ShowDialog();
            if (result == DialogResult.OK)
            {
                if (File.Exists(openFile.FileName))
                {
                    args["browser"] = openFile.FileName;
                }
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (Elevated(args))
            {
                var arguments = new Dictionary<string, string>();

                SetDefaults(arguments);
                ReadINI(arguments);
                ReadCommandLine(args, arguments);
                WriteINI(arguments);

                string path = arguments["path"],
                    browser = arguments["browser"],
                    portDef = arguments["port"];

                int port;
                if (!int.TryParse(portDef, out port))
                {
                    Console.Error.WriteLine("Invalide Port specification. Was {0}, expecting an intenger like 80, 81, 8080, 8383, etc.", portDef);
                }
                else if (!Directory.Exists(path))
                {
                    Console.Error.WriteLine("No directory from which to serve found at {0}", path);
                }
                else if (!File.Exists(browser) && browser != "explorer")
                {
                    Console.Error.WriteLine("No file found for browster at path {0}", browser);
                }
                else {
                    Console.WriteLine("Serving path '{0}', port '{1}'", path, port);
                    var server = StartServer(path, browser, port);
                    while (!server.Done)
                    {
                        var key = Console.ReadKey();
                        if (key.Key == ConsoleKey.X)
                        {
                            server.Stop();
                        }
                        else if (key.Key == ConsoleKey.B)
                        {
                            ShowFindBrowserDialog(arguments);
                            browser = arguments["browser"];
                            WriteINI(arguments);
                            StartBrowser(browser, port);
                        }
                    }
                    Console.WriteLine("Goodbye!");
                    if (openFile != null)
                    {
                        openFile.Dispose();
                    }
                }
            }
        }
    }
}
