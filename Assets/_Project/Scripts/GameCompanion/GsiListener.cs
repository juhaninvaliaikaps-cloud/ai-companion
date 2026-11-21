using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace AICompanion.GameCompanion
{
    /// <summary>
    /// JUH-48: receives the game's Game State Integration posts (CS2, Dota 2) on this PC only
    /// (127.0.0.1). A post without this install's token is dropped, so another program cannot
    /// pose as the game. Bodies wait in <see cref="Pending"/> for the main thread; nothing here
    /// touches Unity. Plain C#, started and stopped by the caller.
    /// </summary>
    public sealed class GsiListener : IDisposable
    {
        public const int MaxBodyBytes = 256 * 1024;
        public const int MaxPending = 64;

        readonly string _token;
        readonly ConcurrentQueue<string> _pending = new ConcurrentQueue<string>();
        HttpListener _listener;
        Thread _thread;
        volatile bool _running;

        public GsiListener(int port, string token)
        {
            Port = port;
            _token = token ?? string.Empty;
        }

        public int Port { get; private set; }

        public bool Running
        {
            get { return _running; }
        }

        /// <summary>Accepted bodies, oldest first. The main thread drains this.</summary>
        public ConcurrentQueue<string> Pending
        {
            get { return _pending; }
        }

        public int Dropped { get; private set; }

        public string Prefix
        {
            get { return "http://127.0.0.1:" + Port + "/"; }
        }

        /// <summary>False when the port is taken or listening is not allowed.</summary>
        public bool Start()
        {
            if (_running)
                return true;
            if (_token.Length == 0)
                return false;
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(Prefix);
                _listener.Start();
            }
            catch (Exception)
            {
                _listener = null;
                return false;
            }

            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "GSI listener" };
            _thread.Start();
            return true;
        }

        public void Stop()
        {
            _running = false;
            try
            {
                if (_listener != null)
                    _listener.Close();
            }
            catch (Exception)
            {
            }

            _listener = null;
        }

        public void Dispose()
        {
            Stop();
        }

        void Loop()
        {
            while (_running)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext();
                }
                catch (Exception)
                {
                    break;
                }

                try
                {
                    Handle(context);
                }
                catch (Exception)
                {
                    Dropped++;
                }
            }

            _running = false;
        }

        void Handle(HttpListenerContext context)
        {
            var request = context.Request;
            var ok = string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && IPAddress.IsLoopback(request.RemoteEndPoint.Address)
                && request.ContentLength64 <= MaxBodyBytes;
            string body = null;
            if (ok)
            {
                using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
                    body = Read(reader);
                ok = body != null && GsiConfig.HasToken(body, _token);
            }

            if (ok && _pending.Count < MaxPending)
                _pending.Enqueue(body);
            else
                Dropped++;

            context.Response.StatusCode = ok ? 200 : 403;
            context.Response.ContentLength64 = 0;
            context.Response.Close();
        }

        static string Read(StreamReader reader)
        {
            var buffer = new char[4096];
            var sb = new StringBuilder();
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                sb.Append(buffer, 0, read);
                if (sb.Length > MaxBodyBytes)
                    return null;
            }

            return sb.ToString();
        }
    }
}
