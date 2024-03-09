using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Net;
using proset.Models;

namespace proset.Controllers;

public class RoomApiController : Controller {
    private readonly ILogger<RoomApiController> _logger;
    private readonly SqlContext _context;

    public RoomApiController(ILogger<RoomApiController> logger, SqlContext context) {
        _logger = logger;
        _context = context;
    }
    
    [HttpGet]
    public async Task SSE(string? room_id) {
        if (room_id is null) {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        Response.Headers.Add("Content-Type", "text/event-stream");
        Response.StatusCode = (int)HttpStatusCode.OK;

        _logger.LogInformation("a");
        
        for (int i = 0; true; i++) {
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
        }
    }
}
