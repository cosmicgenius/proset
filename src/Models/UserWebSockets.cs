using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace proset.Models;

public class UserWebSockets {
    // Maps users (based on user id) to their corresponding web socket connection
    private readonly ConcurrentDictionary<string, WebSocket> _sockets;
    private ConcurrentDictionary<string, ArraySegment<byte>> _buffers;

    public UserWebSockets() {
        _sockets = new ConcurrentDictionary<string, WebSocket>();
        _buffers = new ConcurrentDictionary<string, ArraySegment<byte>>();
    }

    public void AddWebSocket(string user_id, WebSocket socket) {
        _sockets.TryAdd(user_id, socket);
        _buffers.TryAdd(user_id, new ArraySegment<byte>(new byte[1024 * 4]));
    }

    public async Task<string?> ReceiveAsync(string user_id) {
        if (!_sockets.TryGetValue(user_id, out WebSocket? socket) || 
                !_buffers.TryGetValue(user_id, out ArraySegment<byte> buffer)) {
            return null;
        }

        if (socket.State != WebSocketState.Open) {
            await RemoveWebSocketAsync(user_id);
            return null;
        }

        var receiveResult = await socket.ReceiveAsync(buffer, CancellationToken.None);
        if (receiveResult.CloseStatus.HasValue) {
            return null;
        }
        return buffer.ToString();
    }

    public async Task SendAsync(string user_id, string message) {
        if (!_sockets.TryGetValue(user_id, out WebSocket? socket)) return;
        if (socket.State != WebSocketState.Open) {
            await RemoveWebSocketAsync(user_id);
            return;
        }

        await SendMessageAsync(socket, message);
    }

    private async Task SendMessageAsync(WebSocket socket, string message) {
        await socket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
    }

    public async Task RemoveWebSocketAsync(string user_id) {
        if (!_sockets.TryRemove(user_id, out WebSocket? socket)) return;
        if (socket?.State == WebSocketState.Open) {
            await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, 
                    "Goodbye", 
                    CancellationToken.None
                );
        }
        socket?.Dispose();
    }
}
