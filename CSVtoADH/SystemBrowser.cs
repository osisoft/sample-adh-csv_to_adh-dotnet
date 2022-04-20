using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IdentityModel.OidcClient.Browser;

namespace CSVtoADH
{
    public class SystemBrowser : IBrowser
    {
        private readonly string _path;

        public SystemBrowser(int? port = null, string path = null)
        {
            _path = path;

            if (!port.HasValue)
            {
                Port = GetRandomUnusedPort();
            }
            else
            {
                Port = port.Value;
            }
        }

        public static string UserName { get; set; }
        public static string Password { get; set; }
        public static IOpenBrowser OpenBrowser { get; set; }

        public int Port { get; }

        public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = new CancellationToken())
        {
            if (options == null)
            {
                throw new ArgumentException("Options cannot be null.", nameof(options));
            }

            using LoopbackHttpListener listener = new (Port, _path);
            OpenBrowser.OpenBrowser(options.StartUrl, UserName, Password);

            try
            {
                string result = await listener.WaitForCallbackAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(result))
                {
                    return new BrowserResult { ResultType = BrowserResultType.UnknownError, Error = "Empty response." };
                }

                return new BrowserResult { Response = result, ResultType = BrowserResultType.Success };
            }
            catch (TaskCanceledException ex)
            {
                return new BrowserResult { ResultType = BrowserResultType.Timeout, Error = ex.Message };
            }
        }

        private static int GetRandomUnusedPort()
        {
            TcpListener listener = new (IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
