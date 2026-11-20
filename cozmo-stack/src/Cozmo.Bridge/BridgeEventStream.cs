using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cozmo.Bridge;

/// <summary>Read-only, newline-delimited JSON on IPv4 loopback. One subscription per connection.</summary>
public sealed class BridgeEventStream : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private sealed record Subscription(long? Since = null, string[]? Types = null, bool SignificantOnly = false);

    private readonly CancellationTokenSource shutdown;
    private readonly TcpListener listener;
    private readonly Task accepting;

    public BridgeEventStream(CozmoBridge bridge, int port, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(bridge);
        listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        shutdown = CancellationTokenSource.CreateLinkedTokenSource(token);
        accepting = AcceptAsync(bridge, shutdown.Token);
    }

    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    private async Task AcceptAsync(CozmoBridge bridge, CancellationToken token)
    {
        var clients = new List<Task>();
        try
        {
            while (!token.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                clients.RemoveAll(task => task.IsCompletedSuccessfully);
                clients.Add(ServeAsync(bridge, client, token));
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            shutdown.Cancel();
            listener.Stop();
            await Task.WhenAll(clients).ConfigureAwait(false);
        }
    }

    private static async Task ServeAsync(CozmoBridge bridge, TcpClient client, CancellationToken token)
    {
        using (client)
        using (var connection = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            using var stream = client.GetStream();
            using var input = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            Task? readClosed = null;
            var connectionToken = connection.Token;
            try
            {
                using var handshake = CancellationTokenSource.CreateLinkedTokenSource(connectionToken);
                handshake.CancelAfter(TimeSpan.FromSeconds(5));
                string? line = await ReadSubscriptionLine(input, handshake.Token).ConfigureAwait(false);
                if (line is null) return;
                var subscription = JsonSerializer.Deserialize<Subscription>(line, Json)
                    ?? throw new JsonException("Expected a subscription object.");
                if (subscription.Types?.Any(string.IsNullOrWhiteSpace) == true)
                    throw new JsonException("Event types must be nonempty strings.");
                readClosed = WatchClient(input, connection);
                await foreach (var page in bridge.StreamEventsAsync(subscription.Since, subscription.Types,
                    subscription.SignificantOnly, connectionToken).ConfigureAwait(false))
                    await WriteLine(stream, page, connectionToken).ConfigureAwait(false);
            }
            catch (Exception error) when (error is JsonException or ArgumentOutOfRangeException)
            {
                try
                {
                    await WriteLine(stream, new { error = error.Message }, connectionToken).ConfigureAwait(false);
                }
                catch (IOException) { }
                catch (OperationCanceledException) when (connectionToken.IsCancellationRequested) { }
            }
            catch (IOException) { }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            finally
            {
                connection.Cancel();
                if (readClosed is not null) await readClosed.ConfigureAwait(false);
            }
        }
    }

    private static ValueTask WriteLine<T>(NetworkStream stream, T value, CancellationToken token) =>
        stream.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json) + "\n"), token);

    private static async Task<string?> ReadSubscriptionLine(StreamReader reader, CancellationToken token)
    {
        var line = new StringBuilder();
        var character = new char[1];
        while (await reader.ReadAsync(character.AsMemory(), token).ConfigureAwait(false) != 0)
        {
            if (character[0] == '\n') return line.ToString();
            if (line.Length == 4096) throw new JsonException("Subscription exceeds 4096 characters.");
            line.Append(character[0]);
        }
        return null;
    }

    private static async Task WatchClient(StreamReader reader, CancellationTokenSource connection)
    {
        try
        {
            // EOF or further input ends the subscription. This also retires quiet clients.
            await reader.ReadAsync(new char[1].AsMemory(), connection.Token).ConfigureAwait(false);
            connection.Cancel();
        }
        catch (IOException) { connection.Cancel(); }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    public async ValueTask DisposeAsync()
    {
        shutdown.Cancel();
        try { await accepting.ConfigureAwait(false); }
        finally { shutdown.Dispose(); }
    }
}
