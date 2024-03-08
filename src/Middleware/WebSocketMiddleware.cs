using System.Net;
using proset.Models;

namespace proset.Middleware;

public class WebSocketMiddleware {
    private readonly RequestDelegate _next;
    private readonly UserWebSockets _user_sockets;

    public WebSocketMiddleware(
        RequestDelegate next,
        UserWebSockets user_sockets) {
        _next = next;
        _user_sockets = user_sockets;
    }

    public async Task InvokeAsync(HttpContext context, SqlContext db_context) {
        if (context.Request.Path != "/ws") {
            await _next(context);
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest) {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsync("This endpoint accepts only websocket connections.");
            return;
        }

        if (!context.Request.Cookies.TryGetValue("user_id", out string? user_id)
                || user_id is null) {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsync("No user_id cookie found. Are your cookies enabled?");
        }

        _user_sockets.AddWebSocket(user_id ?? "", await context.WebSockets.AcceptWebSocketAsync());

        string? message = await _user_sockets.ReceiveAsync(user_id ?? "");
        while (!(message is null)) {
            
            message = await _user_sockets.ReceiveAsync(user_id ?? "");
        }

        await _user_sockets.RemoveWebSocketAsync(user_id ?? "");
    }
}
