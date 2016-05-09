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
            {"-p", "port" },
            {"/P", "port" },

            {"--browser", "browser" },
            {"-b", "browser" },
            {"/B", "browser" },

            {"--directory", "path" },
            {"-d", "path" },
            {"/D", "path" },

            {"--mode", "mode" },
            {"-m", "mode" },
            {"/M", "mode" }
        };

        static Dictionary<string, KeyValuePair<string, string>> COMMAND_SHORTCUTS = new Dictionary<string, KeyValuePair<string, string>>()
        {
            {"--kiosk", new KeyValuePair<string, string>("mode", "kiosk" ) },
            {"-k", new KeyValuePair<string, string>("mode", "kiosk" ) },
            {"/K", new KeyValuePair<string, string>("mode", "kiosk" ) },

            {"--help", new KeyValuePair<string, string>("help", "help") },
            {"/?", new KeyValuePair<string, string>("help", "help") }
        };

        static Dictionary<string, string> DEFAULT_ARGUMENTS = new Dictionary<string, string>()
        {
            {"port", "80" },
            {"browser", "explorer" },
            {"path", Environment.CurrentDirectory },
            {"mode", "default" }
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
                return Path.Combine(Environment.CurrentDirectory, "httpd.ini");
            }
        }

        static void ReadINI(Dictionary<string, string> args)
        {
            if (File.Exists(SettingsFile))
            {
                var lines = File.ReadAllLines(SettingsFile);
                for (var i = 0; i < lines.Length; ++i)
                {
                    var sep = lines[i].IndexOf('=');
                    if (sep > -1)
                    {
                        var key = lines[i].Substring(0, sep);
                        var value = lines[i].Substring(sep + 1);
                        args[key] = value;
                    }
                }
            }
        }

        static void WriteINI(Dictionary<string, string> args)
        {
            var output = new List<string>();
            foreach (var key in args.Keys)
            {
                if (!(DEFAULT_ARGUMENTS.ContainsKey(key) && DEFAULT_ARGUMENTS[key] == args[key]))
                {
                    output.Add(string.Format("{0}={1}", key, args[key]));
                }
            }
            if (output.Count > 0)
            {
                File.WriteAllLines(SettingsFile, output.ToArray());
            }
            else if (File.Exists(SettingsFile))
            {
                File.Delete(SettingsFile);
            }
        }

        static void ReadCommandLine(string[] args, Dictionary<string, string> arguments)
        {
            for (int i = 0; i < args.Length; ++i)
            {
                if ((args[i].Length > 0 && (args[i][0] == '"' || args[i][0] == '\'') && args[i][0] == args[i][args[i].Length - 1]))
                {
                    args[i] = args[i].Substring(1, args[i].Length - 2);
                }

                if (COMMAND_ALIASES.ContainsKey(args[i]))
                {
                    if (i < args.Length - 1)
                    {
                        arguments[COMMAND_ALIASES[args[i]]] = args[i + 1];
                        ++i;
                    }
                    else
                    {
                        Console.Error.WriteLine("Unknown command switch: {0}.", args[i]);
                        return;
                    }
                }
                else if (COMMAND_SHORTCUTS.ContainsKey(args[i]))
                {
                    var pair = COMMAND_SHORTCUTS[args[i]];
                    arguments[pair.Key] = pair.Value;
                }
                else if (Directory.Exists(args[i]))
                {
                    arguments["path"] = args[i];
                }
                else
                {
                    Console.Error.WriteLine("Missing value for {0} switch.", args[i]);
                    return;
                }
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

        private static void StartBrowser(string browser, int port, string mode)
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

            var parameters = new List<string>();
            if (mode == "kiosk")
            {
                if (browser.Contains("chrome"))
                {
                    parameters.Add("--kiosk");
                }
                else if (browser.Contains("iexplore"))
                {
                    parameters.Add("-k");
                }
            }
            parameters.Add(url);
            StartProc(browser, false, parameters.ToArray());
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
            var arguments = new Dictionary<string, string>();

            SetDefaults(arguments);
            ReadINI(arguments);
            ReadCommandLine(args, arguments);

            if (arguments.ContainsKey("help"))
            {
                Console.WriteLine(@"Starts a basic, static HTTP server. Useful for developing and test web sites locally. DO NOT RUN IN PRODUCTION!

StartHere [(--help|/?)] [(--port|-p|/P) portValue] [(--browser|-b|/B) browserPath] [(--mode|-m|\M) startMode] [(--kiosk|-k|/K)] [(--directory|-d|/D)][dirPath]

    --help              This help text.
    /?                  Alias for --help.

    --port              Specify a port value on which to listen.
    -p                  Alias for --port.
    /P                  Alies for --port.
    portValue           An integer greater than or equal to 80 and less than 65536. Defaults to 80.

    --browser           Specify an alternative browser to use to start URLs.
    -b                  An alias for --browser.
    /B                  An alias for --browser.
    browserPath         A relative or fully qualified path to the browser executable. Defaults to using the Windows Shell to open the URL.

    --mode              Specify the start mode for the Chrome or Internet Explorer. Firefox is not available.
    -m                  An alias for --mode.
    /M                  An alies for -m.
    startMode           Set to 'kiosk' to start in full screen mode. Defaults to no kiosk mode.
    --kiosk             An alias for '--mode kiosk'.
    -k                  An alias for '--mode kiosk'.
    /K                  An alias for '--mode kiosk'.

    --directory         Specify the path where the files are located that should be served. The command switch is not necessary for specifying the path in the last argument position.
    -d                  An alias for --directory.
    /D                  An alias for --directory.
    dirPath             A relative or fully qualified path to a directory full of web content files.

Any settings away from the default will cause a 'httpd.ini' file to be written in the current directory, recording them for the next invocation.");
            }
            else
            {
                WriteINI(arguments);

                string path = arguments["path"],
                    browser = arguments["browser"],
                    portDef = arguments["port"],
                    mode = arguments["mode"];

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
                    Console.Error.WriteLine("No file found for browser at path {0}", browser);
                }
                else if (mode != "default" && mode != "kiosk")
                {
                    Console.Error.WriteLine("Unknown mode '{0}'", mode);
                }
                else if (Elevated(args))
                {
                    Console.WriteLine("Serving path '{0}'", path);
                    SimpleHTTPServer server = null;
                    for (int p = port; p < 0xffff; ++p)
                    {
                        Console.WriteLine("Trying port '{0}'", p);
                        try
                        {
                            server = new SimpleHTTPServer(path, p);
                            port = p;
                            arguments["port"] = p.ToString();
                            StartBrowser(browser, port, mode);
                            break;
                        }
                        catch
                        {
                            Console.Error.WriteLine("Port {0} is already in use. Trying another one.", p);
                        }
                    }
                    if (server != null)
                    {
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
                                StartBrowser(browser, port, mode);
                            }
                        }
                        Console.WriteLine("Goodbye!");
                    }
                    else
                    {
                        Console.Error.WriteLine("Couldn't find an open port.");
                    }
                    if (openFile != null)
                    {
                        openFile.Dispose();
                    }
                }
            }
        }
    }
}
