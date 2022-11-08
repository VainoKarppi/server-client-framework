using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;


namespace ServerFramework {
    public class Program {

        public static void OnClientConnected(object sender, OnClientConnectEvent eventData){
            Console.WriteLine($"CLIENT CONNECTED! ({eventData.UserName} ID:{eventData.Id})");
        }
        public static void OnClientDisconnect(object sender, OnClientDisconnectEvent eventData){
            Console.WriteLine($"CLIENT DISCONNECTED! ({eventData.UserName} ID:{eventData.Id} SUCCESS:{eventData.Success})");
        }
        public static void OnServerShutdown(object sender, OnServerShutdownEvent eventData){
            Console.WriteLine($"SERVER STOPPED! SUCCESS:{eventData.Success}");
        }
        public static void OnMessageSent(object sender, OnMessageSentEvent eventData){
            Console.WriteLine($"MSG SENT: {eventData.Message.MethodName}");
        }
        public static void OnMessageReceived(object sender, OnMessageReceivedEvent eventData){
            Console.WriteLine($"MSG RECEIVED: {eventData.Message.MethodName}");
        }
        

        static void Main(string[] args) {
            NetworkEvents.eventsListener = new NetworkEvents();
            NetworkEvents.eventsListener.ClientConnected += OnClientConnected;
            NetworkEvents.eventsListener.ClientDisconnect += OnClientDisconnect;
            NetworkEvents.eventsListener.ServerShutdown += OnServerShutdown;
            NetworkEvents.eventsListener.MessageSent += OnMessageSent;
            NetworkEvents.eventsListener.MessageReceived += OnMessageReceived;

            
            Console.Title = "SERVER";
            Console.Clear();
            Console.WriteLine("Type 'help' for commands!");
            
            
            int port = 5001;
            bool start = false;
            foreach (var item in args) {
                string[] splitted = item.Split(':');
                if (splitted.Count() == 0) continue;
                string a = splitted[0];
                switch (a.ToLower())
                {
                    case "--port": {
                        if (splitted.Count() != 2) continue;
                        Int32.TryParse(splitted[1],out port);
                        break;
                    }
                    case "--start": {
                        start = true;
                        break;
                    }
                    default: {
                        continue;
                    }
                }
            }
            if (start) {
                Network.StartServer(port);
            }

            while (true) {
                Console.Write("> ");
                string command = Console.ReadLine();
                command = command.ToLower();
            
                try {
                    if (command == "help")
                        Commands.Help();

                    else if (command == "clear")
                        Console.Clear();

                    else if (command == "exit")
                        break;

                    else if (command == "start") {
                        if (Network.ServerRunning) throw new Exception("Server already running!");
                        Console.WriteLine("Enter server port:");
                        string portNew = Console.ReadLine();
                        if (String.IsNullOrEmpty(portNew)) portNew = "5001";
                        Network.StartServer(Int32.Parse(portNew));

                    }
                        

                    else if (command == "stop") {
                        if (!Network.ServerRunning) throw new Exception("Server not running!");
                        Network.StopServer();
                    }

                    else if (command == "users")
                        Commands.UserList();

                    else if (command == "senddata")
                        Commands.SendData();

                    else if (command == "clientmethods")
                        Commands.GetClientMethods();
                    
                    else if (command == "servermethods")
                        Commands.GetServerMethods();

                    else if (command == "requestdata")
                        Commands.RequestData();

                    else if (command == "requestdatatype")
                        Commands.RequestDataType();

                    else if (command == "status")
                        Console.WriteLine(Network.ServerRunning ? "Server is running!" : "Server is not running!");
                        
                    else
                        Console.WriteLine("Unknown command!" + "\n" + "Type 'help' for commands!");

                } catch (Exception e) {
                    Console.WriteLine(e.Message);
                }
                Console.WriteLine();
            }
        }
    }
}