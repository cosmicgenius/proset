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
            await Response.WriteAsync($"data: { JsonSerializer.Serialize(new {
                    cards = 7,
                    current_cards = new List<int> { 1, 2, 3, 5, 8, 13, 21 },
                    game_type = "proset",
                    tokens = 6,
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
