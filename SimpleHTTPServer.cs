using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;


class SimpleHTTPServer
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
    private int port;
    public bool Done
    {
        get;
        private set;
    }

    public int Port
    {
        get { return port; }
    }

    /// <summary>
    /// Construct server with given port.
    /// </summary>
    /// <param name="path">Directory path to serve.</param>
    /// <param name="port">Port of the server.</param>
    public SimpleHTTPServer(string path, int port)
    {
        this.Initialize(path, port);
    }

    /// <summary>
    /// Stop server and dispose all functions.
    /// </summary>
    public void Stop()
    {
        serverThread.Abort();
        listener.Stop();
        this.Done = true;
    }

    public event EventHandler Ready;

    private void Listen()
    {
        listener = new HttpListener();
        listener.Prefixes.Add(string.Format("http://*:{0}/", port));
        try
        {
            listener.Start();
            if (this.Ready != null)
            {
                this.Ready.Invoke(this, EventArgs.Empty);
            }
            while (true)
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
        catch
        {
            Console.Error.WriteLine("Port {0} is already in use. Please try another one.", port);
            this.Done = true;
        }
    }

    private void Process(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        string requestPath = request.Url.AbsolutePath;
        Console.Write(requestPath);
        if (request.HttpMethod != "GET")
        {
            Error(response, HttpStatusCode.MethodNotAllowed, "Method not allowed.");
        }
        else
        {
            requestPath = requestPath.Substring(1);

            if (requestPath.Length > 0 && requestPath[requestPath.Length - 1] == '/')
            {
                requestPath = requestPath.Substring(0, requestPath.Length - 1);
            }

            requestPath = requestPath.Replace('/', Path.DirectorySeparatorChar);
            var filename = Path.Combine(rootDirectory, requestPath);
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

            Console.Write(" --> {0} --> ", filename);

            if (File.Exists(filename))
            {
                try
                {
                    Stream input = new FileStream(filename, FileMode.Open);

                    //Adding permanent http response headers
                    var ext = Path.GetExtension(filename);
                    response.ContentType = MIME.ContainsKey(ext) ? MIME[ext] : "application/octet-stream";
                    response.ContentLength64 = input.Length;
                    response.AddHeader("Date", DateTime.Now.ToString("r"));
                    response.AddHeader("Last-Modified", File.GetLastWriteTime(filename).ToString("r"));

                    byte[] buffer = new byte[1024 * 16];
                    int nbytes;
                    while ((nbytes = input.Read(buffer, 0, buffer.Length)) > 0)
                        response.OutputStream.Write(buffer, 0, nbytes);
                    input.Close();

                    response.StatusCode = (int)HttpStatusCode.OK;
                    Console.WriteLine(HttpStatusCode.OK);
                }
                catch
                {
                    Error(response, HttpStatusCode.InternalServerError, "ERRRRRROR: '{0}'", filename.Replace(this.rootDirectory, ""));
                }
            }
            else
            {
                Error(response, HttpStatusCode.NotFound, "Not found: '{0}'", filename.Replace(this.rootDirectory, ""));
            }
        }

        response.OutputStream.Flush();
        response.OutputStream.Close();
    }

    void Error(HttpListenerResponse response, HttpStatusCode code, string format, params string[] args)
    {
        Console.WriteLine(code);
        response.StatusCode = (int)code;

        using (var writer = new StreamWriter(response.OutputStream))
        {
            writer.WriteLine(format, args);
        }
    }

    private void Initialize(string path, int port)
    {
        this.rootDirectory = path;
        this.port = port;
        serverThread = new Thread(this.Listen);
        serverThread.Start();
    }
}