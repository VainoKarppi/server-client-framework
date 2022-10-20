
using System;
using System.Net.Mime;
using System.Reflection;





namespace ClientFramework {
    public class Program {
        public const int Version = 1000;
        
        
        public static void OnClientConnected(object sender, params object[] parameters){
            int id = (int)parameters[0];
            string username = (string)parameters[1];
            Console.WriteLine($"CLIENT CONNECTED! ({username} ID:{id})");
        }
        public static void OnClientDisconnect(object sender, params object[] parameters){
            int id = (int)parameters[0];
            string username = (string)parameters[1];
            Console.WriteLine($"CLIENT DISCONNECTED! ({username} ID:{id})");
        }
        public static void Main(string[] args) {
            ServerEvents.eventsListener = new ServerEvents();
            ServerEvents.eventsListener.ClientConnected += OnClientConnected;
            ServerEvents.eventsListener.ClientDisconnect += OnClientDisconnect;
            
            //bl.StartProcess();

            Console.WriteLine();
            Console.Title = "EDEN Online Extension CLIENT";
            Console.WriteLine("Type 'help' for commands!");

            Network.Connect("127.0.0.1",2302,"vaino");

            while (true) {
                Console.WriteLine();
                string? command = Console.ReadLine();
                command = command.ToLower();


                try {
                    switch (command)
                    {
                        case "help":
                            Commands.Help();
                            break;
                        case "clear":
                            Console.Clear();
                            break;
                        case "exit":
                            Environment.Exit(0);
                            break;
                        case "connect":
                            Console.WriteLine("Enter IP adress:");
                            string ip = Console.ReadLine();
                            Console.WriteLine("Username:");
                            string name = Console.ReadLine();
                            Network.Connect(ip,2302,name);
                            break;
                        case "disconnect":
                            Network.Disconnect();
                            break;
                        case "users":
                            Commands.UserList();
                            break;
                        case "senddata":
                            Commands.SendData();
                            break;
                        case "requestdata":
                            Commands.RequestData();
                            break;
                        case "status":
                            Console.WriteLine(Network.IsConnected ? "Connected to server! ID:" + Network.Client.Id.ToString() : "NOT connected to server!");
                            break;
                        default:
                            Console.WriteLine("Unknown command!" + "\n" + "Type 'help' for commands!");
                            break;
                    }  
                } catch (Exception e) {
                    Console.WriteLine(e.Message);
                }
            }
        }
    }
}