using System.ComponentModel.DataAnnotations;

namespace proset.Models {
    public enum GameType {
        proset = 0,
        conset = 1
    }

    public class Game {
        [Key]
        public string room_id { get; set; } = "";
        
        public int num_cards { get; set; }
        public int num_tokens { get; set; }

        public GameType game_type { get; set; }

        public List<int> card_order { get; set; } = new List<int>();
    }
}
