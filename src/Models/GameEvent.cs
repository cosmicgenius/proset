namespace proset.Models;

public enum GameEventType {
    GAME_STATE = 1,
    PLAYER_STATE = 2,
}

public class GameEvent {
    public GameEventType event_type { get; protected set; }
}

public class GameStateEvent : GameEvent {
    public int num_cards { get; set; }
    public int num_tokens { get; set; }

    public GameType game_type { get; set; }

    public List<int> current_cards { get; set; } = new List<int>();

    public GameStateEvent() { event_type = GameEventType.GAME_STATE; }
}

public class PlayerStateEvent : GameEvent {
    public List<string> players { get; set; } = new List<string>();
    public List<int> scores { get; set; } = new List<int>();

    public PlayerStateEvent() { event_type = GameEventType.PLAYER_STATE; }
}



