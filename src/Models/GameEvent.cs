namespace proset.Models;

public class GameEvent {
    public int num_cards { get; set; }
    public int num_tokens { get; set; }

    public GameType game_type { get; set; }

    public List<int> current_cards { get; set; } = new List<int>();
}
