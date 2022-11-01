
using System;
using System.Net.Mime;
using System.Reflection;





namespace ClientFramework {
    
    public class TestClass {
        public bool Test { get; set; }
        public string? StringTest { get; set; }
        public dynamic? Data { get; set; }
    }
    public class Program {
        public const int Version = 1000;
        
        
        public static void OnClientConnected(object sender, OnClientConnect client){
            Console.WriteLine($"CLIENT CONNECTED! ({client.UserName} ID:{client.Id})");
        }
        public static void OnClientDisconnect(object sender, OnClientDisconnect client){
            Console.WriteLine($"CLIENT DISCONNECTED! ({client.UserName} ID:{client.Id} SUCCESS:{client.Success})");
        }
        public static void Main(string[] args) {
            ServerEvents.eventsListener = new ServerEvents();
            ServerEvents.eventsListener.ClientConnected += OnClientConnected;
            ServerEvents.eventsListener.ClientDisconnect += OnClientDisconnect;
            
            //bl.StartProcess();

            Console.WriteLine();
            Console.Title = "EDEN Online Extension CLIENT";
            Console.WriteLine("Type 'help' for commands!");

            //Network.Connect("127.0.0.1",2302,"vaino");

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
                            if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";
                            Console.WriteLine("Username:");
                            string name = Console.ReadLine();
                            if (string.IsNullOrEmpty(name)) {
                                Random rd = new Random();
                                name = ("RANDOMUSER" + rd.Next(1,10).ToString());
                            }
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
                        case "requestdatatype":
                            Commands.RequestDataType();
                            break;
                        case "status":
                            Console.WriteLine(Network.Client.HandshakeDone ? "Connected to server! ID:" + Network.Client.Id.ToString() : "NOT connected to server!");
                            break;
                        default:
                            Console.WriteLine("Unknown command!\nType 'help' for commands!");
                            break;
                    }  
                } catch (Exception e) {
                    Console.WriteLine(e.Message);
                }
            }
        }
    }
}