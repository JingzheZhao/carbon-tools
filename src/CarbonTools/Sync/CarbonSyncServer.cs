using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

namespace CarbonTools.Sync
{
    /// <summary>
    /// Minimal local HTTP server embedded in the CC Sync component
    /// (docs/decision-record.md section 16: a local server inside the component,
/// rather than Speckle).
    /// Serves the current sync JSON at /data.json, the carbon dataset under /data/,
    /// and the viewer at /.
    /// The transport can later be swapped for WebSocket without touching the protocol.
    /// </summary>
    public class CarbonSyncServer : IDisposable
    {
        private HttpListener _listener;
        private Thread _thread;
        private volatile string _json = "{}";
        private volatile string _statusLine = "0 objects";

        private static readonly string _viewerHtml = LoadResource("WebAssets.viewer.html");
        private static readonly string _ccDataset  = LoadResource("Data.cc_dataset.json");
        private static readonly string _materials  = LoadResource("Data.materials.json");

        /// <summary>
        /// Preferred port. Browser local storage is scoped to origin, which includes
        /// the port, so a port that changed on every start silently orphaned anything
        /// the viewer had saved. Keeping it stable is what makes saved schemes survive
        /// a Rhino restart.
        /// </summary>
        private const int PreferredPort = 51275;

        public int Port { get; private set; } = -1;

        public bool IsRunning => _listener != null && _listener.IsListening;

        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            Port = ResolvePort();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{Port}/");
            _listener.Start();

            _thread = new Thread(Listen) { IsBackground = true };
            _thread.Start();
        }

        public void Stop()
        {
            if (_listener == null)
            {
                return;
            }

            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch (ObjectDisposedException)
            {
            }

            _listener = null;
            Port = -1;
        }

        public void UpdateData(string json, string statusLine)
        {
            _json = json;
            _statusLine = statusLine;
        }

        private void Listen()
        {
            var listener = _listener;
            while (listener != null && listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = listener.GetContext();
                }
                catch (Exception)
                {
                    break;
                }

                try
                {
                    HandleRequest(context);
                }
                catch (Exception)
                {
                    // Ignore malformed requests, keep serving.
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var path = context.Request.Url.AbsolutePath;

            if (path == "/data.json")
            {
                // the live sync payload from Rhino, not the carbon dataset
                WriteResponse(context, "application/json", _json);
            }
            else if (path == "/data/cc.js")
            {
                // a plain script tag keeps the viewer's start-up order synchronous
                WriteResponse(context, "application/javascript", "const CC=" + Dataset() + ";");
            }
            else if (path == "/data/materials.js")
            {
                WriteResponse(context, "application/javascript", "const MAT=" + Materials() + ";");
            }
            else if (path == "/data/cc_dataset.json")
            {
                WriteResponse(context, "application/json", Dataset());
            }
            else if (path == "/data/materials.json")
            {
                WriteResponse(context, "application/json", Materials());
            }
            else
            {
                WriteResponse(context, "text/html", DevViewerHtml() ?? _viewerHtml);
            }
        }

        /// <summary>
        /// Development override. If CARBONTOOLS_VIEWER points at a viewer.html on disk,
        /// serve that file and re-read it on every request, so front-end changes only
        /// need a browser refresh instead of rebuilding the .gha and restarting Rhino.
        /// Returns null in normal use, and the embedded resource is used instead.
        /// </summary>
        private static string DevViewerHtml()
        {
            return DevFile(Environment.GetEnvironmentVariable("CARBONTOOLS_VIEWER"));
        }

        private static string Dataset()
        {
            return DevData("cc_dataset.json") ?? _ccDataset;
        }

        private static string Materials()
        {
            return DevData("materials.json") ?? _materials;
        }

        /// <summary>
        /// Development override for the dataset, mirroring CARBONTOOLS_VIEWER.
        /// Point CARBONTOOLS_DATA at the repository's data folder to edit carbon
        /// factors and see them on a browser refresh.
        /// </summary>
        private static string DevData(string fileName)
        {
            var dir = Environment.GetEnvironmentVariable("CARBONTOOLS_DATA");
            return string.IsNullOrWhiteSpace(dir)
                ? null
                : DevFile(Path.Combine(dir, fileName));
        }

        private static string DevFile(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return null;
                }

                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch
            {
                // never let a bad dev path break the server
                return null;
            }
        }

        private static string LoadResource(string suffix)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "CarbonTools." + suffix;

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    // a missing resource is a packaging fault, not a runtime one;
                    // say so in a form the caller can render
                    return suffix.EndsWith(".json")
                        ? "{}"
                        : "<html><body>Embedded resource " + suffix + " not found.</body></html>";
                }

                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static void WriteResponse(HttpListenerContext context, string contentType, string body)
        {
            var buffer = Encoding.UTF8.GetBytes(body);
            context.Response.ContentType = contentType;
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }

        /// <summary>
        /// Use the same port every time so the viewer's saved state is still there
        /// tomorrow, but never fail to start because something else holds it.
        /// </summary>
        private static int ResolvePort()
        {
            if (IsPortFree(PreferredPort))
            {
                return PreferredPort;
            }

            return FindFreePort();
        }

        private static bool IsPortFree(int port)
        {
            TcpListener probe = null;
            try
            {
                probe = new TcpListener(IPAddress.Loopback, port);
                probe.Start();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            finally
            {
                if (probe != null)
                {
                    try { probe.Stop(); } catch { }
                }
            }
        }

        private static int FindFreePort()
        {
            var tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();
            return port;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
