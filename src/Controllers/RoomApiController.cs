using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using System.Reflection;
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
    public async Task SSE(string? room_id) {
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
        // Stupid linter
        user_id ??= "";

        User? current_user = await _context.Users.SingleOrDefaultAsync(u => u.user_id == user_id);

        // User not in database or currently in a different room, 
        if (current_user is null || current_user.room_id != room_id) {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        Response.Headers.Add("Content-Type", "text/event-stream");
        Response.Headers.Add("Connection", "Keep-Alive");
        Response.StatusCode = (int)HttpStatusCode.OK;

        _logger.LogInformation("a");

        Game? current_game = await _context.Games.SingleOrDefaultAsync(g => g.room_id == room_id);

        if (current_game is null) {
            current_game = CreateNewGame(room_id, current_game);
            await _context.SaveChangesAsync();
        }

        GameEventSubscriber subscriber = new GameEventSubscriber(Response, user_id);
        _sse_emitter.Subscribe(room_id, subscriber);

        await Response.WriteAsync($"data: { JsonSerializer.Serialize(new GameStateEvent {
                num_cards = current_game.num_cards,
                current_cards = current_game.cards_left > current_game.num_cards ? 
                    (current_game.card_order.Where(c => c > 0)
                    .Take(current_game.num_cards).ToList()) : new List<int>(),
                game_type = current_game.game_type,
                num_tokens = current_game.num_tokens,
            }) }\r\r");
        await EmitPlayerEvent(room_id);
        await _sse_emitter.Flush(room_id);

        // Keep alive, and also remove connection after 1 minute of disconnected
        for (int i = 0; true; i++) {
            // Broadcast exit
            subscriber.CheckAlive();
            if (!subscriber.alive) {
                await EmitPlayerEvent(room_id);
                await _sse_emitter.Flush(room_id);

                return;
            }
            _logger.LogInformation($"{user_id} Connected: {i} min");

            await Task.Delay(1 * 60 * 1000);
        }
    }

    [HttpPost]
    public async Task Found(string? room_id, [FromBody]RoomPost room_post) {
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
                || room_post.cards.Exists(cur => cur < 0) && room_post.cards.Count > 0) {
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
        current_game.cards_left -= room_post.cards.Count;
        current_user.score++;
        await _context.SaveChangesAsync();

        await EmitGameEvent(room_id, current_game);
        await EmitPlayerEvent(room_id);
        await _sse_emitter.Flush(room_id);
    }

    [HttpPost]
    public async Task NewGame(string? room_id) {
        if (room_id is null) {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        // We don't check for the user to be valid here,
        // hopefully this isn't really something that can be abused
        Game? current_game = await _context.Games.SingleOrDefaultAsync(g => g.room_id == room_id);
        current_game = CreateNewGame(room_id, current_game);
        await _context.SaveChangesAsync();

        await EmitGameEvent(room_id, current_game);
    }

    private async Task EmitGameEvent(string room_id, Game current_game) {
        GameStateEvent game_state_event = new GameStateEvent {
                num_cards = current_game.num_cards,
                current_cards = current_game.cards_left > current_game.num_cards ? 
                    (current_game.card_order.Where(c => c > 0)
                    .Take(current_game.num_cards).ToList()) : new List<int>(),
                game_type = current_game.game_type,
                num_tokens = current_game.num_tokens,
            };
        await _sse_emitter.Emit(room_id, JsonSerializer.Serialize(game_state_event));
    }

    private async Task EmitPlayerEvent(string room_id) {
        // Only show players that are actually connected (i.e. playing)
        HashSet<string> active_player_ids = _sse_emitter.GetSubscribers(room_id)?.ToHashSet() ?? new HashSet<string>();
        List<User> players = _context.Users.Where<User>(
                u => u.room_id == room_id && active_player_ids.Contains(u.user_id)
            ).ToList();
        players.Sort(delegate (User lhs, User rhs) {
                if (lhs.username == rhs.username) {
                    return String.Compare(lhs.user_id, rhs.user_id);
                }
                return String.Compare(lhs.username, rhs.username);
            });

        PlayerStateEvent player_state_event = new PlayerStateEvent {
                players = players.Select(u => u.username).ToList(),
                scores = players.Select(u => u.score).ToList(),
            };
        await _sse_emitter.Emit(room_id, JsonSerializer.Serialize(player_state_event));
    }

    // Creates a new game
    // If old_game is null, adds the new game to the database, and returns the new game
    // If old_game is not null, modifies the old game and returns a copy of the new game
    private Game CreateNewGame(string room_id, Game? old_game) {
        Random rng = new Random();

        int num_tokens = 6;
        List<int> card_order = Enumerable.Range(1, (1 << num_tokens) - 1).ToList();

        // Dirty Fisher-Yates shuffle
        // because dotnet core doesn't have this built in ??
        for (int i = card_order.Count - 1; i > 0; i--) {
            int j = rng.Next(i + 1);
            int tmp = card_order[i];
            card_order[i] = card_order[j];
            card_order[j] = tmp;
        }

        Game new_game = new Game() {
            room_id = room_id,
            num_cards = 7,
            num_tokens = 6,
            game_type = GameType.proset,
            card_order = card_order,
            cards_left = (1 << num_tokens) - 1,
        };
        if (old_game is null) {
            _context.Games.Add(new_game);
        } else {
            foreach (PropertyInfo property in 
                    typeof(Game).GetProperties().Where(p => p.CanWrite)) {
                property.SetValue(old_game, property.GetValue(new_game, null), null);
            }
        }
        return new_game;
    }
}
