
namespace ServerFramework {
    public class BaseEventClass {
        public string? EventName { get; set; }
        public BaseEventClass() {
            EventName = this.GetType().UnderlyingSystemType.Name;
        }
    }
    public class OnClientConnect : BaseEventClass {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public bool Success { get; set; } = true;
        public OnClientConnect (int id, string username) {
            Id = id;
            UserName = username;
        }
    }
    public class OnClientDisconnect : BaseEventClass {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public bool Success { get; set; }
        public OnClientDisconnect (int id, string username, bool success = false) {
            Id = id;
            UserName = username;
            Success = success;
        }
    }
    public class OnServerShutdown : BaseEventClass {
        public bool Success { get; set; } = false;
        public OnServerShutdown (bool success) {
            Success = success;
        }
    } 
}
