using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
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
    public async Task SSE(string? room_id) {
        if (room_id is null) {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        Response.Headers.Add("Content-Type", "text/event-stream");
        Response.Headers.Add("Connection", "Keep-Alive");
        Response.StatusCode = (int)HttpStatusCode.OK;

        _logger.LogInformation("a");

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

        GameEventSubscriber subscriber = new GameEventSubscriber(Response, Guid.NewGuid().ToString());
        _sse_emitter.Subscribe(room_id, subscriber);

        // Keep alive, and also remove connection after 1 minute of disconnected
        for (int i = 0; true; i++) {
            if (subscriber.alive == false) {
                return;
            }
            _logger.LogInformation($"Connected: {i} min");

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
}
