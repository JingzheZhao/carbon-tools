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
    /// (E:\Carbon\Carbon工具-方法论决策记录.md section 16: "自研 GH 组件内嵌本地服务，不用 Speckle").
    /// Serves the current sync JSON at /data.json and a placeholder page at /.
    /// The transport can later be swapped for WebSocket without touching the protocol.
    /// </summary>
    public class CarbonSyncServer : IDisposable
    {
        private HttpListener _listener;
        private Thread _thread;
        private volatile string _json = "{}";
        private volatile string _statusLine = "0 objects";

        private static readonly string _viewerHtml = LoadViewerHtml();

        public int Port { get; private set; } = -1;

        public bool IsRunning => _listener != null && _listener.IsListening;

        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            Port = FindFreePort();
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
                WriteResponse(context, "application/json", _json);
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
            try
            {
                var path = Environment.GetEnvironmentVariable("CARBONTOOLS_VIEWER");
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

        private static string LoadViewerHtml()
        {
            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "CarbonTools.WebAssets.viewer.html";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    return "<html><body>viewer.html embedded resource not found.</body></html>";
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
