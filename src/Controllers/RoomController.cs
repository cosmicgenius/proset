using Microsoft.AspNetCore.Mvc;
using proset.Models;

namespace proset.Controllers;

public class RoomController : Controller {
    private readonly ILogger<RoomController> _logger;
    private readonly SqlContext _context;

    public RoomController(ILogger<RoomController> logger, SqlContext context) {
        _logger = logger;
        _context = context;
    }
    
    [HttpGet]
    public IActionResult Index(string? room_id) {
        if (room_id is null) {
            return View("Error", new ErrorViewModel { Reason = "room is null" }); // TODO
        }

        if (!Request.Cookies.TryGetValue("user_id", out string? user_id) 
                || user_id is null || user_id.Length > 36) {

            // If they have no cookie, give them a free cookie, and then 
            // redirect them to the add room page
            user_id = Guid.NewGuid().ToString();
            Response.Cookies.Append("user_id", user_id, new CookieOptions { HttpOnly = true });
            return View("MoveRoom", new MoveRoomView { room_id = room_id, user_id = user_id });
        }

        User? current_user = _context.Users.SingleOrDefault<User>(u => u.user_id == user_id);

        // User not in database or currently in a different room, 
        // so they must be added/switch first
        if (current_user is null || current_user.room_id != room_id) {
            return View("MoveRoom", new MoveRoomView { 
                    room_id = room_id, 
                    user_id = user_id,
                });
        }
        
        /*
        List<string> player_usernames = _context.Users.Where<User>(u => u.room_id == room_id)
            .Select(u => u.username).ToList() ?? new List<string>();
        */

        return View(new RoomView { 
                room_id = room_id, 
                user_id = user_id,
                username = current_user.username
                //player_usernames = player_usernames
            });
    }
    
    // Technically should be PUT, but we can't PUT with forms, so this is POST
    [HttpPost]
    public async Task<IActionResult> Index(MoveRoom move_room) {
        // Shouldn't happen
        if (_context.Users is null) {
            return View("Error", new ErrorViewModel { Reason = "Database not initialized" }); // TODO
        }

        User? current_user = _context.Users?.SingleOrDefault<User>(
                u => u.user_id == move_room.user_id
            );
        if (current_user is null) {
            // Add
            // Stupid linter
            if (!(_context.Users is null)) {
                await _context.Users.AddAsync(new User { 
                        user_id = move_room.user_id, 
                        username = move_room.username, 
                        room_id = move_room.room_id, 
                        score = 0
                    });
                await _context.SaveChangesAsync();
            }
        } else {
            // Update
            current_user.room_id = move_room.room_id;
            current_user.username = move_room.username;
            current_user.score = 0;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Index", new { room_id = move_room.room_id });
    }

}
