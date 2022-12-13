
using System.Reflection.Metadata;
using System.Data;
using System.Collections;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;


#if SERVER
using static ServerFramework.Logger;
using static ServerFramework.NetworkEvents;
using static ServerFramework.Network;
namespace ServerFramework;
#else
using static ClientFramework.Logger;
using static ClientFramework.NetworkEvents;
using static ClientFramework.Network;
namespace ClientFramework;
#endif

//!! SHARED METHODS


/// <summary></summary>
public partial class Network {
	///<summary>List of the methods available on SERVER ( Registered using "M:ServerFramework.Network.RegisterMethod")</summary>
	#if SERVER
	public static readonly List<MethodInfo> ServerMethods = new List<MethodInfo>();
	#else
	public static readonly List<NetworkMethodInfo> ServerMethods = new List<NetworkMethodInfo>();
	#endif

	///<summary>List of the methods available on CLIENT ( Registered using "M:ClientFramework.Network.RegisterMethod")</summary>
	#if SERVER
	public static readonly List<NetworkMethodInfo> ClientMethods = new List<NetworkMethodInfo>();
	#else
	public static readonly List<MethodInfo> ClientMethods = new List<MethodInfo>();
	#endif

	private static Dictionary<int,dynamic> Results = new Dictionary<int,dynamic>();
	private static List<string> PrivateMethods = new List<string>() {};
    private static bool MethodsInitialized = false;


    /// <summary>Verion of the server. example: "1.0.0.0". Gets its value after successfull handshake</summary>
    public static string ServerVersion { get; private set; } = "1.0.0.0";

	/// <summary>List of the other clients connected to server</summary>
	#if SERVER
	public static readonly List<NetworkClient> ClientList = new List<NetworkClient>();
	#else
    public static readonly List<OtherClient> ClientList = new List<OtherClient>();
	#endif


	#if SERVER
	private static int? ClientID = 1;
	#else
	private static int? ClientID;
	#endif

	/// <summary>Check if the current Network namespace is using server framework</summary>
	#if SERVER
	public const bool IsServer = true;
	#else
	public const bool IsServer = false;
	#endif



	/// <summary>Create new instance of a Network Event to be executed on client(s)</summary>
	public class NetworkEvent {
		public int MessageType { get; set; } = 10;
		/// <summary>Array of targets. Use negative int to remove from list. {0 = everyone} {-2 = everyone else expect client 2} {-5,-6,...}</summary>
		public int[]? Targets { get; set; } = new int[] { 0 };
		///<summary>Class instance of event to be sent. T:ServerFramework.NetworkEvents</summary>
		public dynamic? EventClass { get; set; }
		///<summary>NetworkEvent to be invoked on client</summary>
		public NetworkEvent(dynamic eventClass) {
			this.EventClass = eventClass;
		}
		///<summary>Empty NetworkEvent to be invoked on client. Requires at least EventClass</summary>
		public NetworkEvent() {}
	}
	
	/// <summary>Message types to be used in NetworkMessage and NetworkEvent</summary>
	public enum MessageTypes : int {
		/// <summary>Used for fire and forget</summary>
		SendData,
		/// <summary>Used for requesting data from target</summary>
		RequestData
	}

	/// <summary>Create new instance of a Network Event to be executed on client(s)</summary>
	public class NetworkMessage {
		internal int? Hash;
		///<summary>Type of the message (use MessageTypes)</summary>
		public int? MessageType { get; set; } = 0;
		///<summary>Target ID who to send the message to. (0 = everyone, 1 = server, 2 = clientID, -2 = Everyone excpect client 2)</summary>
		public int? TargetId { get; set; } = 0;
		///<summary>Name of the registered method on receivers end</summary>
		public string? MethodName { get; set; }
		///<summary>Parameters to be send into the method</summary>
		public dynamic? Parameters { get; set; }
		///<summary>Check if Parameters is a class object or object</summary>
		public bool UseClass { get; set; }
		///<summary>Random Key that is used to check for response for specific request</summary>
		public int Key { get; set; } = new Random().Next(100,int.MaxValue);
		///<summary>ID of the client who sent the message</summary>
		public int? Sender { get; set; } = ClientID;
		// Id of the sender. Can be null in case handshake is not completed
		public bool? isHandshake { get; set; }
		// Used to detect for handshake. Else send error for not connected to server!
		internal dynamic? OriginalParams { get; set; }
		/// <summary>Builds a new NetworkMessage that can be sent to wanted target using SendData or RequestData</summary>
		public NetworkMessage() {
			this.Hash = this.GetHashCode(); // TODO Check if same as on client
		}
		/// <summary>Builds a new NetworkMessage that can be sent to wanted target using SendData or RequestData</summary>
		public NetworkMessage(string methodName, int targetID, dynamic? parameters = null) {
			this.MethodName = methodName;
			this.TargetId = targetID;
			this.Parameters = parameters;
			this.Hash = this.GetHashCode(); // TODO Check if same as on client
		}
	}

	/// <summary>Method info that is available on client.</summary>
	public class NetworkMethodInfo {
		/// <summary>Method name</summary>
		public string? Name { get; internal set; }
		/// <summary>Return type of the method</summary>
		public Type? ReturnType { get; internal set; }
		/// <summary>Parameter types in the method</summary>
		public Type[]? Parameters { get; internal set; }
		internal NetworkMethodInfo(string? name, Type? type, Type[]? paramTypes) {
			this.Name = name;
			this.ReturnType = type;
			this.Parameters = paramTypes;
		}
	}

	



	/// <summary>
	/// Request data from target by invoking its method and parse to wanted type. (Can be class instance)
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="message"></param>
	/// <returns></returns>
	/// <exception cref="NullReferenceException"></exception>
	/// <exception cref="InvalidOperationException"></exception>
	/// <exception cref="Exception"></exception>
	public static dynamic RequestData<T>(NetworkMessage message) {
		dynamic returnMessage = RequestData(message);
		if (returnMessage is JsonElement) {
			var returned = ((JsonElement)returnMessage[1]).Deserialize<T>();
			if (returned == null) throw new NullReferenceException();
			return returned;
		}
		return (T)returnMessage;
	}
	

	
	/// <summary>
	/// Request data from target by invoking its method and parse to wanted type. (Can be class instance) using ASYNC
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="message"></param>
	/// <returns></returns>
	/// <exception cref="NullReferenceException"></exception>
	/// <exception cref="InvalidOperationException"></exception>
	/// <exception cref="Exception"></exception>
	public static async Task<dynamic> RequestDataAsync<T>(NetworkMessage message) {
		dynamic returnMessage = await RequestData(message);
		if (returnMessage is JsonElement) {
			var returned = ((JsonElement)returnMessage[1]).Deserialize<T>();
			if (returned == null) throw new NullReferenceException();
			return returned;
		}
		return (T)returnMessage;
	}
	
	
	
	/// <summary>
	/// Request data from target by invoking its method using ASYNC
	/// </summary>
	/// <param name="message"></param>
	/// <returns></returns>
	/// <exception cref="InvalidOperationException"></exception>
	/// <exception cref="Exception"></exception>
	public static async Task<dynamic> RequestDataAsync(NetworkMessage message) {
		return await RequestData(message);
	}



	

	/// <summary>
	/// Register method to available methods to be invoked. When client uses SendData or RequestData
	/// </summary>
	/// <param name="className"></param>
	/// <param name="methodName"></param>
	/// <returns>INT : Amount of the registered methods</returns>
	public static int RegisterMethod(Type className, string? methodName = null) {
		try {
			#if SERVER
			if (ServerRunning) throw new Exception("Cannot register new methods while server is running!");
			#else
			if (IsConnected()) throw new Exception("Cannot register new methods while connected to server!");
			#endif
			
			#if SERVER
			var ListToUse = ServerMethods;
			#else
			var ListToUse = ClientMethods;
			#endif

			if (methodName == null) {
                MethodInfo[] methods = className.GetMethods(BindingFlags.Static | BindingFlags.Public);
				int i = 0;
				foreach (var item in methods) {
					if (ListToUse.Contains(item)) continue;
					ListToUse.Add(item);
					i++;
				}
				return i;
			} else {
				MethodInfo? item = className.GetMethod(methodName,BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase);
				if (item == null) return 0;
				if (ListToUse.Contains(item)) return 0;
				ListToUse.Add(item);
				return 1;
			}
		} catch { return -1; }
	}



	/// <summary>
    /// Invoke a event on receivers end.
    /// </summary>
    /// <param name="message"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public static void SendEvent(NetworkEvent message)
    {
#if SERVER
        if (!ServerRunning) throw new InvalidOperationException("Server not running!");

        // Add ALL clients to list if left as blank
        List<int> targets = new List<int>();
        if (message.Targets == null) message.Targets = new int[] { 0 };

        // Exclusive targeting [-2] = everyone else expect client 2

        if (message.Targets.Count() == 1)
        {
            int target = message.Targets[0];
            if (target < 0)
            {
                foreach (NetworkClient client in ClientList)
                {
                    if (target * -1 != client.ID) targets.Add(client.ID);
                }
            }
            else
            {
                foreach (NetworkClient client in ClientList) targets.Add(client.ID);
            }
        }
        else
        {
            targets = message.Targets.ToList();
            foreach (int target in message.Targets)
            { // [-4,-5,...]
                if (target < 0 && targets.Contains(target * -1)) targets.Remove(target);
            }
        }

        // Send to single or multiple users
        message.Targets = null; // Dont send targets over net
        foreach (int id in targets)
        {
            NetworkClient? client = ClientList.FirstOrDefault(c => c.ID == id);
            if (client == null || client == default) continue;
            SendMessage(message, client.Stream);
        }


#else
        if (!Client.HandshakeDone) throw new Exception("Server not running!");

        // Add ALL clients to list if left as blank
        List<int> targets = new List<int>();
        if (message.Targets == null) message.Targets = new int[] { 0 };
        if (Client.ID != null) message.Targets = message.Targets.Concat(new int[] { (int)Client.ID * -1 }).ToArray(); // Dont send back to client
        SendMessage(message, Client.GetStream());
#endif
    }	



	// TODO do async version!!!
    private static void SendMessage(dynamic message, NetworkStream Stream, bool waitResponse = true)
    {
        if (message is NetworkMessage && message.isHandshake != true && message.Sender != ClientID)
        {
            NetworkEvents? listener = NetworkEvents.Listener;
            listener?.ExecuteEvent(new OnMessageSentEvent(message));
        }
        if (message is NetworkMessage && !(message.Parameters is null) && message.Sender == ClientID)
        {
            bool useClass = false;
            if (message.OriginalParams == null) message.OriginalParams = message.Parameters; // TODO find better way
            message.Parameters = SerializeParameters(message.OriginalParams, ref useClass);
            message.UseClass = useClass;
        }
        // [0 = ack, 1-4 = JsonMsgLenght, 5-6 = ACK KEY, 7... actual JsonMsg]
        List<byte> bytes = new List<byte>();
		bytes.AddRange(JsonSerializer.SerializeToUtf8Bytes(message));
        bytes.InsertRange(0,BitConverter.GetBytes(bytes.Count)); // 4 bytes
        
        // Always add extra random key (2 bytes)
        bytes.Insert(0,Convert.ToByte(waitResponse)); // 0 = NACK : 1 = ACK
        Random rand = new Random();
        ushort randomKey = (ushort)rand.Next(65530);
        bytes.InsertRange(5,BitConverter.GetBytes(randomKey));

        // Send data
        Stream.WriteAsync(bytes.ToArray(), 0, bytes.Count);

        if (waitResponse) {
            new Thread(() => {
                int timer = 0;
                while (timer < 100) {
                    if (Results.ContainsKey((int)randomKey)) return; // ACK received!
                    Thread.Sleep(1); // TODO ASYNC
                    ++timer;
                }
                Log($"ERROR: ACK not received for: {message} (MSG timed out!)");
            }).Start();
        }
    }


    /// <summary>
    /// Request data from target by invoking its method using ASYNC
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="Exception"></exception>
    public static async void SendDataAsync(NetworkMessage message) {
		await Task.Run(() => { SendData(message); });
	}

	private static byte[] ReadMessageBytes(NetworkStream Stream) {
        List<byte> bytes = new List<byte>();

        byte[] byteInfo = new byte[7];
		Stream.Read(byteInfo,0,7);

        byte ackByte = byteInfo[0];
        int msgLenght = BitConverter.ToInt32(byteInfo,1);
		ushort ackKey = BitConverter.ToUInt16(byteInfo,5);

        if (ackByte == 0x06) {
            Results.Add((int)ackKey,true);
            return new byte[] { 0x06 }; // IS ACTUAL ACK RECEIVED
        }

		byte[] msgBytes = new byte[msgLenght];
		Stream.Read(msgBytes,0,msgLenght);
        Stream.Flush();

        if (ackByte == 0x01) { // Send ACK of msg Received
            byte[] responseBytes = {0x06,0x00,0x00,0x00,0x00,(byte)(ackKey),(byte)(ackKey >> 8)};
            Stream.WriteAsync(responseBytes);
        }
		return msgBytes;
	}
	private static dynamic RequestDataResult(NetworkMessage message) {
		dynamic returnMessage;
		short timer = 0;
		while (true) {
			Thread.Sleep(1);
			if (Results.ContainsKey(message.Key)) {
				returnMessage = (Results.First(n => n.Key == message.Key)).Value; // Just in case if the index changes in between
				Results.Remove(message.Key);
				break;
			}
			if (timer > 100) throw new Exception($"Request {message.Key} ({message.MethodName}) timed out!");
			timer++;
		}
		return returnMessage;
	}

	private static MethodInfo? GetMessageMethodInfo(string? methodName) {
        int methodId;
        MethodInfo? method;
        bool isInt = int.TryParse(methodName, out methodId);
        if (isInt && (methodId < 0)) {
            string privateMethodName = PrivateMethods[Math.Abs(methodId) - 1];
            method = typeof(Network).GetMethod(privateMethodName);
        } else {

		#if SERVER
			method = ServerMethods.FirstOrDefault(x => x.Name.ToLower() == methodName?.ToLower());
		#else
            method = ClientMethods.FirstOrDefault(x => x.Name.ToLower() == methodName?.ToLower());
		#endif
		
		    if (method == default) throw new Exception($"Method {methodName} was not found from Registered Methods!");
        }
        return method;
    }

	private static object[]? GetMessageParameters(MethodInfo? method, dynamic message, dynamic? client = null) {
        ParameterInfo[]? parameterInfo = method?.GetParameters(); // Get parameters from the method to be invoked
        if (parameterInfo?.Count() > 0) {
            List<object> paramList = new List<object>();
            ParameterInfo first = parameterInfo[0];

		#if SERVER
			if (first.ParameterType == typeof(NetworkClient)) paramList.Add(client);
			if (parameterInfo.Count() > 1 && (parameterInfo[1].ParameterType == typeof(NetworkMessage))) paramList.Add(message);
		#else
            if (first.ParameterType == typeof(NetworkMessage)) paramList.Add(message);
		#endif

            if (message.Parameters != null) {
                if (message.Parameters is Array){
                    foreach (var item in message.Parameters){
                        if (method?.GetParameters().Count() == paramList.Count()) break; // Not all parameters can fill in
                        paramList.Add(item);
                    }
                } else {
                    paramList.Add(message.Parameters);
                }
            }
            return paramList.ToArray();
        }
        return null;
    }

	private static object[] SerializeParameters(dynamic parameters,ref bool useClass) {
		try {
			Type paramType = Type.GetType(parameters.ToString());
			useClass = paramType.GetConstructor(new Type[0])!=null;
			if (useClass) return parameters;
		} catch {}
		
		List<object> newParams = new List<object>(){};
		if (!(parameters is Array)) {
			newParams.Add(parameters.GetType().ToString());
			newParams.Add(parameters);
		} else {
			newParams.Add(parameters.GetType().ToString());
			foreach (var parameter in parameters) {
				newParams.Add(parameter.GetType().ToString());
				newParams.Add(parameter);
			}
		}
		return newParams.ToArray();
	}
	private static dynamic? DeserializeParameters(dynamic parameterData, bool useClass = false) {
		if(parameterData is null) return null;
		if (useClass) return parameterData;
		List<object> parameters = JsonSerializer.Deserialize<List<object>>(parameterData);
		bool odd = parameters.Count()%2 != 0;
		if (odd && parameters.Count() > 2) {
			parameters.RemoveAt(0);
		}
		Type? type = default;
		List<object?> final = new List<object?>();
		for (int i = 0; i < parameters.Count(); i++) {
			object? value = parameters[i];
			string? valueString = value.ToString();
			if (valueString == null) continue;
			if (i%2 == 0) {
				type = Type.GetType(valueString);
				continue;
			}
			if (type == null) continue;
			if (type.IsArray) {
				object[] data = ParseParamArray(valueString);
				final.Add(data);
			} else {
				object? dataTemp = TypeDescriptor.GetConverter(type).ConvertFromInvariantString(valueString);
				final.Add(dataTemp);
			}
		}
		if (!odd) {
			return final[0];
		}
		return final.ToArray();
	}



	private static void DebugMessage(NetworkMessage message,int mode = 0) {
		Log("===============DEBUG MESSAGE===============");
		string type = "UNKNOWN";
		if (mode == 1) type = "OUTBOUND";
		else if (mode == 2) type = "INBOUND";
		Log($"TYPE: {type}");
		Log($"TIME: {DateTime.Now.Millisecond}");
		Log($"MessageType:{message.MessageType}");
		Log($"TargetId:{message.TargetId}");
		Log($"MethodName:{message.MethodName}");
		Log($"ParamIsClass:{message.UseClass}");
		Log($"Handshake:{message.isHandshake}");
		Log();
		Log(JsonSerializer.Serialize<object>(message.Parameters));
		Log();
		Log($"Key:{message.Key}");
		Log($"Sender:{message.Sender}");
		Log("===============DEBUG MESSAGE===============");
	}


	private static object[] ParseParamArray(string args) {
		if (args.ElementAt(0) != '[') 
			args = "[" + args + "]";
			
		args = args.Substring(1);

		List<object> array = new List<object>();
		char[] nums = new char[] {'0','1','2','3','4','5','6','7','8','9','0','.','-'};
		for (int i = 0; i < args.Length; i++) {
			if (args[i] == '[') {
				StringBuilder str = new StringBuilder();
				int inArr = 0;
				while (true) {
					if (args[i] == '[') inArr++;
					if (args[i] == ']') inArr--;
					str.Append(args[i]);
					i++;
					if (inArr == 0) break;
				}
				object[] innerArray = ParseParamArray(str.ToString());
				array.Add(innerArray);
			} else if (args[i] == '"') {
				StringBuilder str = new StringBuilder();
				bool isEnd = false;
				i++;
				while (true) {
					try {
						if (args[i] == '"') {
							isEnd = !isEnd;
						}
					} catch {
						break;
					}
					if (isEnd && (args[i] == ',' || args[i] == ']')) {
						break;
					}
					str.Append(args[i]);
					i++;
				}
				array.Add(str.ToString().TrimEnd('"'));
			} else if (nums.Contains(args[i])) {
				StringBuilder str = new StringBuilder();
				bool isFloat = false;
				while (nums.Contains(args[i])) {
					if (args[i] == '.')
						isFloat = true;
					str.Append(args[i]);
					i++;
				}
				if (isFloat) {
					double num = Convert.ToDouble(str.ToString());
					array.Add(num);
				} else {
					int num = Convert.ToInt32(str.ToString());
					array.Add(num);
				}
			} else if (
				Substring(args, i, 4).ToLower() == "true") {
				array.Add(true);
				i = i + 4;
			} else if (Substring(args, i, 5).ToLower() == "false") {
				array.Add(false);
				i = i + 5;
			}
		}
		return array.ToArray();
	}
	private static string Substring(string input, int start, int length) {
		int inputLength = input.Length;
		if (start + length >= inputLength) {
			return input.Substring(start);
		}
		return input.Substring(start, length);
	}
}