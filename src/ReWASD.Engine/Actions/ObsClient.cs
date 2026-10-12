using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReWASD.Core.Models;

namespace ReWASD.Engine.Actions;

/// <summary>A minimal OBS WebSocket (protocol v5) client: connect, sign in if OBS asks, send one request, wait for its answer.</summary>
public static class ObsClient
{
    /// <summary>The sign-in answer OBS expects: base64(sha256(base64(sha256(password + salt)) + challenge)).</summary>
    public static string Authentication(string password, string salt, string challenge)
    {
        string secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
    }

    public static async Task SendAsync(string host, int port, string password, ObsRequest request, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        var token = timeout.Token;

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://{host}:{port}"), token);

        // OBS speaks first: Hello (op 0), with a challenge when a password is set
        using (var hello = await ReceiveAsync(socket, token))
        {
            var root = hello.RootElement;
            if (root.GetProperty("op").GetInt32() != 0) throw new InvalidOperationException("OBS did not say hello");

            var identify = new Dictionary<string, object> { ["rpcVersion"] = 1 };
            if (root.GetProperty("d").TryGetProperty("authentication", out var auth))
            {
                if (password.Length == 0) throw new InvalidOperationException("OBS needs a password; set it in Preferences");
                identify["authentication"] = Authentication(password, auth.GetProperty("salt").GetString()!, auth.GetProperty("challenge").GetString()!);
            }
            await SendJsonAsync(socket, new { op = 1, d = identify }, token);
        }

        using (var identified = await ReceiveAsync(socket, token))
        {
            if (identified.RootElement.GetProperty("op").GetInt32() != 2) throw new InvalidOperationException("OBS did not accept the sign-in");
        }

        string id = Guid.NewGuid().ToString("N");
        object data = request.DataJson.Length == 0 ? new { } : JsonSerializer.Deserialize<JsonElement>(request.DataJson);
        await SendJsonAsync(socket, new { op = 6, d = new { requestType = request.RequestType, requestId = id, requestData = data } }, token);

        while (true)
        {
            using var message = await ReceiveAsync(socket, token);
            var root = message.RootElement;
            if (root.GetProperty("op").GetInt32() != 7) continue;     // events and other chatter

            var d = root.GetProperty("d");
            if (d.GetProperty("requestId").GetString() != id) continue;
            if (!d.GetProperty("requestStatus").GetProperty("result").GetBoolean())
            {
                var status = d.GetProperty("requestStatus");
                string comment = status.TryGetProperty("comment", out var c) ? c.GetString() ?? "" : "";
                throw new InvalidOperationException($"OBS refused {request.RequestType}: {comment}");
            }
            break;
        }

        try
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }
        catch (WebSocketException)
        {
            // OBS closing first is fine
        }
    }

    private static Task SendJsonAsync(ClientWebSocket socket, object payload, CancellationToken token) =>
        socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(payload), WebSocketMessageType.Text, true, token);

    private static async Task<JsonDocument> ReceiveAsync(ClientWebSocket socket, CancellationToken token)
    {
        var buffer = new byte[16 * 1024];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, token);
            if (result.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("OBS closed the connection");
            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return JsonDocument.Parse(stream.ToArray());
    }
}
