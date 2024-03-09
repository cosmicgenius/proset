using System.ComponentModel.DataAnnotations;

namespace proset.Models {
    public class User {
        [Key]
        [StringLength(36, ErrorMessage = "The {0} cannot exceed {1} characters.")]
        public string user_id { get; set; } = "";
        public string username { get; set; } = "";
        public string? room_id { get; set; }
        public int score { get; set; }
    }
}
