using System.Text.Json;

namespace proset.Models;

public class TimedHostedService : IHostedService, IDisposable {
    private int executionCount = 0;
    private readonly ILogger<TimedHostedService> _logger;
    private readonly SqlContext _context;
    private readonly IEventEmitter _sse_emitter;
    private Timer? _timer = null;

    public TimedHostedService(ILogger<TimedHostedService> logger, SqlContext context, IEventEmitter sse_emitter) {
        _logger = logger;
        _context = context;
        _sse_emitter = sse_emitter;
    }

    public Task StartAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Timed Hosted Service running.");

        _timer = new Timer(DoWork, null, 0,
            15 * 1000);

        return Task.CompletedTask;
    }

    private void DoWork(object? state) {
        var count = Interlocked.Increment(ref executionCount);

        string room_id = "test";
            
        Game? current_game = _context.Games?.SingleOrDefault(g => g.room_id == room_id);

        // Deal with this later (we should create a new game)
        if (current_game is null) {
            return;
        }

        GameEvent e = new GameEvent {
                num_cards = current_game.num_cards,
                current_cards = current_game.card_order.Where(c => c > 0).Take(current_game.num_cards).ToList(),
                game_type = current_game.game_type,
                num_tokens = current_game.num_cards,
            };

        _sse_emitter.Emit(room_id, JsonSerializer.Serialize(e));
        _logger.LogInformation($"Emit #{count}: room {room_id}, data: {JsonSerializer.Serialize(e)} ");
    }

    public Task StopAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Timed Hosted Service is stopping.");
        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose() {
        _timer?.Dispose();
    }
}
