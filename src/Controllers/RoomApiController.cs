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

    private async Task<Game> NewGame(string room_id) {
        Random rng = new Random();

        int num_tokens = 6;
        List<int> card_order = Enumerable.Range(0, 1 << num_tokens).ToList();

        // Dirty Fisher-Yates shuffle
        // because dotnet core doesn't have this built in ??
        for (int i = card_order.Count - 1; i > 0; i--) {
            int j = rng.Next(i + 1);
            int tmp = card_order[i];
            card_order[i] = card_order[j];
            card_order[j] = tmp;
        }

        Game game = new Game() {
            room_id = room_id,
            num_cards = 7,
            num_tokens = 6,
            game_type = GameType.proset,
            card_order = card_order
        };
        await _context.Games.AddAsync(game);
        await _context.SaveChangesAsync();

        return game;
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

        Game? current_game = await _context.Games.SingleOrDefaultAsync(g => g.room_id == room_id);

        // Deal with this later (we should create a new game)
        if (current_game is null) {
            /*Response.StatusCode = (int)HttpStatusCode.NotImplemented;
            await Response.WriteAsync("No game found");
            return;*/
            current_game = await NewGame(room_id);
        }

        await Response.WriteAsync($"data: { JsonSerializer.Serialize(new {
                num_cards = current_game.num_cards,
                current_cards = current_game.card_order
                    .Where(c => c > 0).Take(current_game.num_cards),
                game_type = current_game.game_type,
                num_tokens = current_game.num_tokens,
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
    }

    [HttpPost]
    [ActionName("SSE")]
    public async Task SSEPost(string? room_id, [FromBody]RoomPost room_post) {
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

        User? current_user = await _context.Users.SingleOrDefaultAsync(u => u.user_id == user_id);

        // User not in database or currently in a different room, 
        if (current_user is null || current_user.room_id != room_id) {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        Game? current_game = await _context.Games.SingleOrDefaultAsync(g => g.room_id == room_id);

        // Trying to post cards into a non existent game
        if (current_game is null) {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        // Check cards are valid 
        if (room_post.cards.Aggregate(0, (acc, cur) => acc ^ cur) != 0 
                || room_post.cards.Exists(cur => cur < 0)) {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        // Modify the current game
        // We cannot know if they are in order, unfortunately, so we must iterate
        foreach (int card in room_post.cards) {
            for (int i = 0; i < current_game.card_order.Count; i++) {
                if (current_game.card_order[i] == card) {
                    current_game.card_order[i] *= -1;
                } else if (current_game.card_order[i] == -card) {
                    Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }
            }
        }
        await _context.SaveChangesAsync();

        GameEvent e = new GameEvent {
                num_cards = current_game.num_cards,
                current_cards = current_game.card_order.Where(c => c > 0)
                    .Take(current_game.num_cards).ToList(),
                game_type = current_game.game_type,
                num_tokens = current_game.num_tokens,
            };

        await _sse_emitter.Emit(room_id, JsonSerializer.Serialize(e));
        _logger.LogInformation($"Emit: Room {room_id}, data: {JsonSerializer.Serialize(e)} ");
    }
}
