using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Threading;

namespace HereTTP
{
    class HTTPServer
    {
        private readonly string[] INDEX_FILES = {
            "index.html",
            "index.htm",
            "default.html",
            "default.htm"
        };

        private static IDictionary<string, string> MIME = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) {
        #region extension to MIME type list
            {".asf", "video/x-ms-asf"},
            {".asx", "video/x-ms-asf"},
            {".avi", "video/x-msvideo"},
            {".bin", "application/octet-stream"},
            {".cco", "application/x-cocoa"},
            {".crt", "application/x-x509-ca-cert"},
            {".css", "text/css"},
            {".deb", "application/octet-stream"},
            {".der", "application/x-x509-ca-cert"},
            {".dll", "application/octet-stream"},
            {".dmg", "application/octet-stream"},
            {".ear", "application/java-archive"},
            {".eot", "application/octet-stream"},
            {".exe", "application/octet-stream"},
            {".flv", "video/x-flv"},
            {".gif", "image/gif"},
            {".hqx", "application/mac-binhex40"},
            {".htc", "text/x-component"},
            {".htm", "text/html"},
            {".html", "text/html"},
            {".ico", "image/x-icon"},
            {".img", "application/octet-stream"},
            {".iso", "application/octet-stream"},
            {".jar", "application/java-archive"},
            {".jardiff", "application/x-java-archive-diff"},
            {".jng", "image/x-jng"},
            {".jnlp", "application/x-java-jnlp-file"},
            {".jpeg", "image/jpeg"},
            {".jpg", "image/jpeg"},
            {".js", "application/x-javascript"},
            {".mml", "text/mathml"},
            {".mng", "video/x-mng"},
            {".mov", "video/quicktime"},
            {".mp3", "audio/mpeg"},
            {".mpeg", "video/mpeg"},
            {".mpg", "video/mpeg"},
            {".msi", "application/octet-stream"},
            {".msm", "application/octet-stream"},
            {".msp", "application/octet-stream"},
            {".pdb", "application/x-pilot"},
            {".pdf", "application/pdf"},
            {".pem", "application/x-x509-ca-cert"},
            {".pl", "application/x-perl"},
            {".pm", "application/x-perl"},
            {".png", "image/png"},
            {".prc", "application/x-pilot"},
            {".ra", "audio/x-realaudio"},
            {".rar", "application/x-rar-compressed"},
            {".rpm", "application/x-redhat-package-manager"},
            {".rss", "text/xml"},
            {".run", "application/x-makeself"},
            {".sea", "application/x-sea"},
            {".shtml", "text/html"},
            {".sit", "application/x-stuffit"},
            {".swf", "application/x-shockwave-flash"},
            {".tcl", "application/x-tcl"},
            {".tk", "application/x-tcl"},
            {".txt", "text/plain"},
            {".war", "application/java-archive"},
            {".wbmp", "image/vnd.wap.wbmp"},
            {".wmv", "video/x-ms-wmv"},
            {".xml", "text/xml"},
            {".xpi", "application/x-xpinstall"},
            {".zip", "application/zip"},
        #endregion
        };

        private Thread serverThread;
        private string rootDirectory;
        private HttpListener listener;

        public int Port
        {
            get;
            private set;
        }

        public bool Done
        {
            get;
            private set;
        }

        /// <summary>
        /// Construct server with given port.
        /// </summary>
        /// <param name="path">Directory path to serve.</param>
        /// <param name="port">Port of the server.</param>
        public HTTPServer(string path, int port)
        {
            rootDirectory = path;
            Port = port;

            listener = new HttpListener();
            listener.Prefixes.Add(string.Format("http://*:{0}/", Port));
            listener.Start();
            serverThread = new Thread(this.Listen);
            serverThread.Start();
        }

        /// <summary>
        /// Stop server and dispose all functions.
        /// </summary>
        public void Stop()
        {
            serverThread.Abort();
            listener.Stop();
            Done = true;
        }

        private void Listen()
        {
            while (!Done)
            {
                try
                {
                    var context = listener.GetContext();
                    Process(context);
                }
                catch
                {

                }
            }
        }

        private void Process(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            if (request.HttpMethod == "GET")
            {
                string requestPath = request.Url.AbsolutePath,
                    requestFile = MassageRequestPath(requestPath),
                    filename = Path.Combine(rootDirectory, requestFile);
                bool isDirectory = Directory.Exists(filename);
                if (isDirectory)
                {
                    filename = FindDefaultFile(filename);
                }
                string shortName = MakeShortName(filename);

                Console.Write(" --> {0} --> ", shortName);

                if (isDirectory && requestPath[requestPath.Length - 1] != '/')
                {
                    Redirect(response, requestPath + "/");
                }
                else if (File.Exists(filename))
                {
                    try
                    {
                        SendFile(response, filename);
                    }
                    catch
                    {
                        Error(response, HttpStatusCode.InternalServerError, "ERRRRRROR: '{0}'", shortName);
                    }
                }
                else
                {
                    Error(response, HttpStatusCode.NotFound, shortName);
                }
            }
            else
            {
                Error(response, HttpStatusCode.MethodNotAllowed, request.HttpMethod);
            }

            response.OutputStream.Flush();
            response.OutputStream.Close();
        }

        private static void Redirect(HttpListenerResponse response, string filename)
        {
            response.AddHeader("Location", filename);
            SetStatus(response, HttpStatusCode.TemporaryRedirect);
        }

        private static void SendFile(HttpListenerResponse response, string filename)
        {
            using (Stream input = new FileStream(filename, FileMode.Open))
            {
                var ext = Path.GetExtension(filename);
                response.ContentType = MIME.ContainsKey(ext) ? MIME[ext] : "application/octet-stream";
                response.ContentLength64 = input.Length;
                response.AddHeader("Date", DateTime.Now.ToString("r"));

                byte[] buffer = new byte[1024 * 16];
                int nbytes;
                while ((nbytes = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    response.OutputStream.Write(buffer, 0, nbytes);
                }
                input.Close();
            }
            SetStatus(response, HttpStatusCode.OK);
        }

        private string MakeShortName(string filename)
        {
            var shortName = filename.Replace(rootDirectory, "");
            if (shortName.Length > 0 && shortName[0] == Path.DirectorySeparatorChar)
            {
                shortName = shortName.Substring(1);
            }

            return shortName;
        }

        private string FindDefaultFile(string filename)
        {
            if (Directory.Exists(filename))
            {
                for (int i = 0; i < INDEX_FILES.Length; ++i)
                {
                    var test = Path.Combine(filename, INDEX_FILES[i]);
                    if (File.Exists(test))
                    {
                        filename = test;
                        break;
                    }
                }
            }

            return filename;
        }

        private static string MassageRequestPath(string requestPath)
        {
            Console.Write(requestPath);
            requestPath = requestPath.Substring(1);

            if (requestPath.Length > 0 && requestPath[requestPath.Length - 1] == '/')
            {
                requestPath = requestPath.Substring(0, requestPath.Length - 1);
            }

            requestPath = requestPath.Replace('/', Path.DirectorySeparatorChar);
            return requestPath;
        }

        void Error(HttpListenerResponse response, HttpStatusCode code, string format, params string[] args)
        {
            Console.Write(format, args);
            SetStatus(response, code);

            using (var writer = new StreamWriter(response.OutputStream))
            {
                writer.WriteLine(format, args);
            }
        }

        private static void SetStatus(HttpListenerResponse response, HttpStatusCode code)
        {
            Console.WriteLine(code);
            response.StatusCode = (int)code;
        }
    }
}