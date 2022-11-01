
namespace ServerFramework {
    public class OnClientConnect {
        public string EventName { get; set; }
        public int Id { get; set; }
        public string? UserName { get; set; }
        public bool Success { get; set; } = true;
        public OnClientConnect (int id, string username) {
            EventName = this.GetType().Name;
            Id = id;
            UserName = username;
        }
    }
    public class OnClientDisconnect {
        public string EventName { get; set; }
        public int Id { get; set; }
        public string? UserName { get; set; }
        public bool Success { get; set; }
        public OnClientDisconnect (int id, string username, bool success) {
            EventName = this.GetType().Name;
            Id = id;
            UserName = username;
            Success = success;
        }
    }
}
