using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using proset.Models;

namespace proset.Controllers;

public class RoomApiController : Controller {
    private readonly ILogger<RoomApiController> _logger;
    private readonly SqlContext _context;
    private readonly IEventEmitter _sse_emitter;

    public RoomApiController(ILogger<RoomApiController> logger, SqlContext context, 
            IEventEmitter sse_emitter) {
        _logger = logger;
        _context = context;
        _sse_emitter = sse_emitter;
    }
    
    [HttpGet]
    [ActionName("SSE")]
    public async Task SSEGet(string? room_id) {
        if (room_id is null) {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await Response.WriteAsync("No room_id provided");
            return;
        }

        if (!Request.Cookies.TryGetValue("user_id", out string? user_id) 
                || user_id is null || user_id.Length > 36) {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await Response.WriteAsync("Missing or invalid user_id cookie");
            return;
        }

        Response.Headers.Add("Content-Type", "text/event-stream");
        Response.Headers.Add("Connection", "Keep-Alive");
        Response.StatusCode = (int)HttpStatusCode.OK;

        _logger.LogInformation("a");

        var task = _context.Games?.SingleOrDefaultAsync(g => g.room_id == room_id);
        Game? current_game = task is null ? null : await task;

        // Deal with this later (we should create a new game)
        if (current_game is null) {
            Response.StatusCode = (int)HttpStatusCode.NotImplemented;
            await Response.WriteAsync("No game found");
            return;
        }

        await Response.WriteAsync($"data: { JsonSerializer.Serialize(new {
                num_cards = current_game.num_cards,
                current_cards = current_game.card_order.Where(c => c > 0).Take(current_game.num_cards),
                game_type = current_game.game_type,
                num_tokens = current_game.num_cards,
            }) }\r\r");
        await Response.Body.FlushAsync();

        GameEventSubscriber subscriber = new GameEventSubscriber(Response, user_id ?? "");
        _sse_emitter.Subscribe(room_id, subscriber);

        // Keep alive, and also remove connection after 1 minute of disconnected
        for (int i = 0; true; i++) {
            if (subscriber.alive == false) {
                return;
            }
            _logger.LogInformation($"{user_id} Connected: {i} min");

            await Task.Delay(1 * 60 * 1000);
        }

        /*for (int i = 0; true; i++) {
            Game? current_game = _context.Games?.SingleOrDefault(g => g.room_id == room_id);

            // Deal with this later (we should create a new game)
            if (current_game is null) {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            await Response.WriteAsync($"data: { JsonSerializer.Serialize(new {
                    num_cards = current_game.num_cards,
                    current_cards = current_game.card_order.Where(c => c > 0).Take(current_game.num_cards),
                    game_type = current_game.game_type,
                    num_tokens = current_game.num_cards,
                }) }\r\r");
            await Response.Body.FlushAsync();

            if (Response.HttpContext.RequestAborted.IsCancellationRequested == true) {
                break;
            }

            _logger.LogInformation($"b {i}");

            await Task.Delay(15 * 1000);
        }*/
    }

    [HttpPost]
    [ActionName("SSE")]
    public async Task SSEPost(string? room_id) {
        if (room_id is null) {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        // No/invalid user_id cookie
        if (!Request.Cookies.TryGetValue("user_id", out string? user_id) 
                || user_id is null || user_id.Length > 36) {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        var task1 = _context.Users?.SingleOrDefaultAsync(u => u.user_id == user_id);
        User? current_user = task1 is null ? null : await task1;

        // User not in database or currently in a different room, 
        if (current_user is null || current_user.room_id != room_id) {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        var task2 = _context.Games?.SingleOrDefaultAsync(g => g.room_id == room_id);
        Game? current_game = task2 is null ? null : await task2;

        // Trying to post cards into a non existent game
        if (current_game is null) {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        // TODO: modify the current game

        GameEvent e = new GameEvent {
                num_cards = current_game.num_cards,
                current_cards = current_game.card_order.Where(c => c > 0).Take(current_game.num_cards).ToList(),
                game_type = current_game.game_type,
                num_tokens = current_game.num_cards,
            };

        await _sse_emitter.Emit(room_id, JsonSerializer.Serialize(e));
        _logger.LogInformation($"Emit: Room {room_id}, data: {JsonSerializer.Serialize(e)} ");
    }
}
