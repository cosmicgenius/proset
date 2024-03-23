namespace proset.Models {
    public class RoomView {
        public string room_id { get; set; } = "";
        public string user_id { get; set; } = "";
        public string username { get; set; } = "";

        public List<string> player_usernames { get; set; } = new List<string>();
    }
}
