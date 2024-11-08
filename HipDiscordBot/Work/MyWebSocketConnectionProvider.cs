using System.Net;
using System.Net.WebSockets;
using NetCord.Gateway.WebSockets;
using WebSocketMessageFlags = NetCord.Gateway.WebSockets.WebSocketMessageFlags;
using WebSocketMessageType = NetCord.Gateway.WebSockets.WebSocketMessageType;

namespace HipDiscordBot.Work;

// NetCord/Gateway/WebSockets/WebSocketConnection.cs
public class MyWebSocketConnection : IWebSocketConnection
{
    private readonly ClientWebSocket _webSocket = new();

    public int? CloseStatus => (int?)_webSocket.CloseStatus;

    public string? CloseStatusDescription => _webSocket.CloseStatusDescription;

    public MyWebSocketConnection(IWebProxy? proxy)
    {
        _webSocket.Options.Proxy = proxy;
    }

    public ValueTask OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        return new(_webSocket.ConnectAsync(uri, cancellationToken));
    }

    public void Abort()
    {
        _webSocket.Abort();
    }

    public ValueTask CloseAsync(int closeStatus, string? closeStatusDescription,
        CancellationToken cancellationToken = default)
    {
        return new(_webSocket.CloseOutputAsync((WebSocketCloseStatus)closeStatus, closeStatusDescription,
            cancellationToken));
    }

    public async ValueTask<WebSocketConnectionReceiveResult> ReceiveAsync(Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        var result = await _webSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
        return new(result.Count, (WebSocketMessageType)result.MessageType, result.EndOfMessage);
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType,
        WebSocketMessageFlags messageFlags, CancellationToken cancellationToken = default)
    {
        return _webSocket.SendAsync(buffer, (System.Net.WebSockets.WebSocketMessageType)messageType,
            (System.Net.WebSockets.WebSocketMessageFlags)messageFlags, cancellationToken);
    }

    public void Dispose()
    {
        _webSocket.Dispose();
    }
}

public class MyWebSocketConnectionProvider : IWebSocketConnectionProvider
{
    private readonly IWebProxy? _proxy;

    public MyWebSocketConnectionProvider(IWebProxy? proxy)
    {
        _proxy = proxy;
    }

    public IWebSocketConnection CreateConnection()
    {
        return new MyWebSocketConnection(_proxy);
    }
}