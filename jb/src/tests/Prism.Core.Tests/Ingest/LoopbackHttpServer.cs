using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PrismCoreTests.Ingest;

/// <summary>
/// Minimal HTTP/1.1 server on a loopback TcpListener for fetcher tests. Raw sockets rather than
/// HttpListener so no HTTP.SYS URL ACL is needed on the CI runner. Answers HEAD and GET from a
/// per-path responder map (Fetch_HTTPS_DirectFile probes with HEAD before downloading with GET)
/// and keeps connections alive so a reused HttpClient connection serves both requests.
/// </summary>
internal sealed class LoopbackHttpServer : IDisposable {
    private readonly TcpListener listener;
    private readonly CancellationTokenSource shutdown = new();
    private readonly Func<string, (int Status, byte[] Body)> responder;

    public int Port { get; }

    public string BaseUrl => $"http://127.0.0.1:{Port}";

    public LoopbackHttpServer(Func<string, (int Status, byte[] Body)> responder) {
        this.responder = responder;
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _ = Task.Run(AcceptClientsAsync);
    }

    private async Task AcceptClientsAsync() {
        while (!shutdown.IsCancellationRequested) {
            TcpClient client;
            try {
                client = await listener.AcceptTcpClientAsync(shutdown.Token);
            }
            catch (OperationCanceledException) {
                return;
            }
            catch (SocketException) {
                return;
            }
            _ = Task.Run(() => HandleConnectionAsync(client));
        }
    }

    private async Task HandleConnectionAsync(TcpClient client) {
        using (client) {
            NetworkStream stream = client.GetStream();
            try {
                while (!shutdown.IsCancellationRequested) {
                    (string method, string path)? request = await ReadRequestHeadAsync(stream);
                    if (request is null) {
                        return;
                    }

                    (int status, byte[] body) = responder(request.Value.path);
                    string reason = status == 200 ? "OK" : "Not Found";
                    byte[] header = Encoding.ASCII.GetBytes(
                        $"HTTP/1.1 {status} {reason}\r\n" +
                        $"Content-Length: {body.Length}\r\n" +
                        "Content-Type: application/octet-stream\r\n" +
                        "Connection: keep-alive\r\n\r\n");

                    await stream.WriteAsync(header, shutdown.Token);
                    if (request.Value.method != "HEAD") {
                        await stream.WriteAsync(body, shutdown.Token);
                    }
                    await stream.FlushAsync(shutdown.Token);
                }
            }
            catch (OperationCanceledException) {
            }
            catch (IOException) {
                // Client closed the connection mid-exchange — normal for HttpClient teardown.
            }
        }
    }

    /// <summary>Reads one request head (through the blank line) and returns its method and path. Null when the peer closed.</summary>
    private async Task<(string method, string path)?> ReadRequestHeadAsync(NetworkStream stream) {
        StringBuilder head = new();
        byte[] one = new byte[1];

        while (!head.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal)) {
            int read = await stream.ReadAsync(one, shutdown.Token);
            if (read == 0) {
                return null;
            }
            head.Append((char)one[0]);
        }

        // Request line: "GET /path HTTP/1.1". HEAD and GET carry no body, so the head is the whole request.
        string requestLine = head.ToString().Split("\r\n", 2)[0];
        string[] parts = requestLine.Split(' ');
        return (parts[0], parts[1]);
    }

    public void Dispose() {
        shutdown.Cancel();
        listener.Stop();
        shutdown.Dispose();
    }
}
