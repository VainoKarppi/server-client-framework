using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;

namespace ClientFramework {
	
    public class Network {
		
		public static short ClientID;
		public static bool IsConnected { get; set; } = false;
		public static TcpClient Client = default!;
		public static Dictionary<ushort,object[]> Results = new Dictionary<ushort,object[]>();

		public static void Connect(string ip = "127.0.0.1", int port = 2302, string userName = "unknown") {
			if (IsConnected)
				throw new Exception("Already connected to server!");

			// Reset variables (Just to be sure)
			Client = new TcpClient();


			Console.WriteLine("Trying to connect at: (" + ip + ":" + port.ToString() + "), with name: " + userName);
			Client.Connect(IPAddress.Parse(ip), port);
			
			// Continue in new thread
			Thread thread = new Thread(ReceiveDataThread);
			thread.Start();

			// Request client ID and do handshake
			Network.Handshake(userName);
		}

		public static void Disconnect() {
			if (!IsConnected)
				throw new Exception("Not connected to server!");

			
			Client.Client.Close();
			IsConnected = false;
		}


		public static string ReadDataBytes(TcpClient client, out byte requestType, out string function, out short target, out ushort key) {
            NetworkStream Stream = client.GetStream();
			byte[] bytes = new byte[client.ReceiveBufferSize];
			int byte_count = Stream.Read(bytes, 0, (int)client.ReceiveBufferSize);

			requestType = bytes[0];
			function = Request.Functions[(bytes[2] << 8) + bytes[1]];
			target = (short)((bytes[4] << 8) + bytes[3]);
			key = (ushort)((bytes[6] << 8) + bytes[5]);

			byte[] newBytes = new byte[byte_count - 7];
			Array.Copy(bytes, 7, newBytes, 0, newBytes.Length);
			return Encoding.ASCII.GetString(newBytes);
		}
		public static void ReceiveDataThread() {
			try {
				IsConnected = true;

				while (IsConnected) {
					
					byte requestType;
					string function;
					short target;
					ushort key;
                    string rawData = ReadDataBytes(Client,out requestType,out function, out target, out key);

					object[] parameters = Request.Deserialize(rawData);

					Console.WriteLine($"*READING* type:{requestType} func:{function} key:{key} target:{target} data:{rawData}");
					Console.WriteLine(parameters);
					foreach (var x in parameters) Console.WriteLine(x);

					// RESPONSE FOR THIS CLIENTS REQUEST
					if (requestType == (byte)Request.MessageTypes.ResponseData && key != 0) {
						// TODO Add response ID
						Results.Add(key,parameters);
						continue;
					}

					// SERVER OR ANOTHER CLIENT REQUESTING DATA FROM THIS CLIENT
					// TODO get data for value (clientFunctions)
					if (requestType == (byte)Request.MessageTypes.RequestData) {
						Network.SendData(function, new object[] {}, target);
						continue;
					}
					
					// DIRECT COMMAND/FUNCTION INCOMING (fire and forget)
					// TODO handle on server
					if (requestType == (byte)Request.MessageTypes.SendData) {
						byte type = (byte)parameters[0];
						if (type == 1) {
							Extension.CallFunction((string)parameters[1],(string)parameters[2]);
						} else {
							throw new NotImplementedException();
						}
						continue;
					}
					Console.WriteLine("*ERROR* Unhandled network message!");
				}
			} catch (Exception ex) {
				if (!IsConnected)
					return;

				if (ex.InnerException is SocketException) {
					Console.WriteLine("Server was shutdown!");
					Extension.CallFunction("disconnect", "0");
					Client.Client.Close();
					IsConnected = false;
				}
				Console.WriteLine(ex.Message);
			}
		}

		public static byte[] PrepareMessage(byte messageType, short target , object function, object[] parameters, ushort key = 0) {
            
            short functionNum = (function.GetType() == typeof(string)) ? GetFunctionIndex(function) : (short)function;

            byte[] msg = {messageType,(byte)(functionNum & 255),(byte)(functionNum >> 8),(byte)(target & 255),(byte)(target >> 8),(byte)(key & 255),(byte)(key >> 8)};
            if (parameters != null || parameters.Count() >= 0) {
                string paramsString = Request.Serialize(parameters);
                msg = msg.Concat(Encoding.ASCII.GetBytes(paramsString)).ToArray();
            }
            return msg;
        }

		// Fire and forget
		public static void SendData(object function, object[] parameters, short target = 0, short excludedTarget = 0, byte messageType = (byte)Request.MessageTypes.SendData) {
            if (!IsConnected)
				throw new Exception("Not connected to server");
			if (target == ClientID)
                throw new Exception("Cannot send data to self!");
			
            byte[] msg = PrepareMessage(messageType,target,function,parameters);
			Client.GetStream().Write(msg, 0, msg.Length);
        }

		// Return data
		public static object[] RequestData(object function, object[] parameters, short target = 1) {
			if (!IsConnected && (string)function != "Handshake") throw new Exception("Not connected to server");

			Random r = new Random();
			ushort key = (ushort)r.Next(1,65500);

			// Send request
			byte[] msg = PrepareMessage(((byte)Request.MessageTypes.RequestData),target,function,parameters,key);
			Client.GetStream().Write(msg, 0, msg.Length);

			
			// Wait for response
			object[] returns;
			short timer = 0;
			while (true) {
				Thread.Sleep(1);
				if (Results.ContainsKey(key)) {
					returns = Results[key];
					Results.Remove(key);
					break;
				}
				if (timer > 100) throw new Exception($"Request {key} ({function}) timed out!");
				timer++;
			}
			return returns;
		}
		
		public static void Handshake(string userName) {
			string version = Program.Version.ToString();
			Console.WriteLine($"Starting HANDSHAKE with server, with version {version}");
			object[] data = RequestData("Handshake",new object[] {version,userName});

			int _clientID = (int)data[0];
			if (_clientID < 0) {
				if (_clientID == -1) throw new Exception("Version mismatch!");
				// Add more checks?

				throw new Exception("Unknown handshake");
			}

			ClientID = (short)_clientID;
		}

		public static short GetFunctionIndex(object function) {

			short functionNum = -1;
			if (function.GetType() == typeof(string))
				functionNum = (short)Array.FindIndex(Request.Functions.ToArray(), t => t.Equals(function.ToString(), StringComparison.InvariantCultureIgnoreCase));
			else if(short.TryParse(function.ToString(),out functionNum))
				functionNum = short.Parse(function.ToString());
			else 
				throw new Exception($"Invalid function type: {function} ({function.GetType()})");

			return functionNum;
		}
	}
	
}
