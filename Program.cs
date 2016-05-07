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
                if (key != "path" && !(DEFAULT_ARGUMENTS.ContainsKey(key) && DEFAULT_ARGUMENTS[key] == args[key]) )
                {
                    output.Add(string.Format("{0}={1}", key, args[key]));
                }
            }
            File.WriteAllLines(SettingsFile, output.ToArray());
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
                var startInfo = new ProcessStartInfo();
                startInfo.UseShellExecute = true;
                startInfo.WorkingDirectory = Environment.CurrentDirectory;
                startInfo.FileName = Application.ExecutablePath;
                startInfo.Verb = "runas";
                startInfo.Arguments = String.Join(" ", args);
                Process p = Process.Start(startInfo);
                return false;
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

                int minPort;
                if (!int.TryParse(arguments["port"], out minPort))
                {
                    Console.Error.WriteLine("Invalide Port specification. Was {0}, expecting an intenger like 80, 81, 8080, 8383, etc.", arguments["port"]);
                }
                else if (!Directory.Exists(arguments["path"]))
                {
                    Console.Error.WriteLine("No directory from which to serve found at {0}", arguments["path"]);
                }
                else if (!File.Exists(arguments["browser"]) && arguments["browser"] != "explorer")
                {
                    Console.Error.WriteLine("No file found for browster at path {0}", arguments["browser"]);
                }
                else {
                    SimpleHTTPServer server = null;
                    for (var port = minPort; port < 0xffff && server == null; ++port)
                    {
                        try
                        {
                            Console.WriteLine("Trying port {0}...", port);
                            server = new SimpleHTTPServer(arguments["path"], port);
                            server.Ready += new EventHandler((o, e) =>
                            {
                                Console.WriteLine("Ready!");
                            });
                        }
                        catch (Exception exp)
                        {
                            Console.Write("Port {0} already taken, ", port);
                        }
                    }
                }
            }
        }
    }
}
