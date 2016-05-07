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
        listener.Prefixes.Add("http://*:" + port.ToString() + "/");
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
 
    private void Process(HttpListenerContext context)
    {
        string filename = context.Request.Url.AbsolutePath;
        Console.WriteLine(filename);
        filename = filename.Substring(1);
 
        if (string.IsNullOrEmpty(filename))
        {
            foreach (string indexFile in INDEX_FILES)
            {
                if (File.Exists(Path.Combine(rootDirectory, indexFile)))
                {
                    filename = indexFile;
                    break;
                }
            }
        }
 
        filename = Path.Combine(rootDirectory, filename);
 
        if (File.Exists(filename))
        {
            try
            {
                Stream input = new FileStream(filename, FileMode.Open);
                
                //Adding permanent http response headers
                string mime;
                context.Response.ContentType = MIME.TryGetValue(Path.GetExtension(filename), out mime) ? mime : "application/octet-stream";
                context.Response.ContentLength64 = input.Length;
                context.Response.AddHeader("Date", DateTime.Now.ToString("r"));
                context.Response.AddHeader("Last-Modified", System.IO.File.GetLastWriteTime(filename).ToString("r"));
 
                byte[] buffer = new byte[1024 * 16];
                int nbytes;
                while ((nbytes = input.Read(buffer, 0, buffer.Length)) > 0)
                    context.Response.OutputStream.Write(buffer, 0, nbytes);
                input.Close();
                
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.OutputStream.Flush();
            }
            catch
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
 
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            using (var writer = new StreamWriter(context.Response.OutputStream))
            {
                writer.WriteLine("Not found: '{0}'", filename.Replace(this.rootDirectory, ""));
                writer.Flush();
            } 
        }
        
        context.Response.OutputStream.Close();
    }
 
    private void Initialize(string path, int port)
    {
        this.rootDirectory = path;
        this.port = port;
        serverThread = new Thread(this.Listen);
        serverThread.Start();
    }
 
 
}