


<h1>Server-client-framework</h1>

This framework is divided to two different namespaces. One for <u>SERVER</u> framework and another for <u>CLIENT</u> framework.

<br>
<h2>Setup using DLL reference</h2>
To get started create new folder project using:<br>

<code>dotnet new console -n MyServerProject</code>

Next recompile the project using ```buildDLL.sh``` or <b>DOWNLOAD</b> one from:<h3>[DOWNLOAD HERE](https://github.com/Razer223/server-client-framework/releases/tag/test)</h3>

<br>
Extract the .zip inside your project folder. The .DLL should be inside:<br>
<code>MyServerProject/server-client-framework.x.x.x.x/server-client-framework.x.x.x.x.dll</code>
<br><br>

Once completed add reference to .dll in .csproj.

```xml
<Project Sdk="Microsoft.NET.Sdk">
    ...
    ...
    <ItemGroup>
        <Reference Include="server-client-framework">
            <HintPath>.\server-client-framework.x.x.x.x\server-client-framework.x.x.x.x.dll</HintPath>
        </Reference>
    </ItemGroup>
</Project>
```
Where X is the version.

Now the reference is done and we can proceed to create our application!

<br>
<h2>Setup using NuGet package</h2>
--TODO

<br><br><br>
<h2>Server Framework Example</h2>

```C#
using ServerFramework;
using static ServerFramework.Network;

Network.RegisterMethod( typeof(ServerMethods) ); // SEE CLASS EXAMPLE BELOW!

Network.StartServer();

// ("methodName", target, parameter(s))
// target: 0 = everyone on server (icludes server), 1 = server, 2 = client with id: 2, -4 = everyone else expect client 4 
NetworkMessage message = new NetworkMesssage("testclient",1,"this is a parameter");
var returnedData = Network.RequestData(message); // "testclient" method returns string! (see client example)
Console.WriteLine($"{returnedData} ({returnedData.GetType()})");


public class ServerMethods {
    public static string TestServer(string testMessage) {
        Console.WriteLine($"MSG:{testMessage}");
        return "Hello MSG RESPONSE From SERVER!";
    }
    // BOTH NetworkClient AND NetworkMessage are OPTIONAL!
    // NetworkClient is available only on server!
    public static int TestIntServer(NetworkClient client, NetworkMessage message, string testMessage) {
        Console.WriteLine($"MSG:{testMessage} ID:{client.ID} MSG:{message.Hash}");
        return 165;
    }
    // Get data using class
    public static TestClass GetClassData(NetworkClient client, object[] parameters) {
        foreach (var item in parameters) Console.WriteLine($"{item} Type:{item.GetType()}");
        return new TestClass(true, "testClass", 12.5);
    }
}

// Has to be exactly the same as on CLIENT!
public class TestClass {
    public bool Test { get; set; }
    public string? Text { get; set; }
    public dynamic? Data { get; set; }
    public TestClass(bool test, string? text, dynamic? anything = null) {
        this.Test = true;
        this.Text = text;
        this.Data = anything;
    }
}
```

<br><br><br>
<h2>Client Framework Example</h2>

```C#
using ClientFramework;
using static ClientFramework.Network;


Network.RegisterMethod( typeof(ClientMethods) ); // SEE CLASS EXAMPLE BELOW!

Network.Connect();

// "GetClassData" that is registered on server
NetworkMessage message = new NetworkMessage("GetClassData",1,new object[] {123,true,"test"});
TestClass returnedClass = Network.RequestData<TestClass>(message); // Make pointer for TestClass Type
Console.WriteLine($"{returnedClass.Text} ({(returnedClass.Data)?.GetType()})");


public class ClientMethods {
    // NetworkMessage is optional!
    public static string TestClient(NetworkMessage message, dynamic testData) {
        Console.WriteLine($"MSG:{testData} type:({testData.GetType()})");
        return "Hello MSG RESPONSE From CLIENT!";
    }
}

// Return value of: Type.GetType("TestClass"); has to be same on both client and server!!
public class TestClass {
    public bool Test { get; set; }
    public string? Text { get; set; }
    public dynamic? Data { get; set; }
    public TestClass(bool test, string? text, dynamic? anything = null) {
        this.Test = true;
        this.Text = text;
        this.Data = anything;
    }
}
```

<br>
<h2>Events</h2>
Events are executed on both server and/or client. Some events might differ depending where its being executed at. For example OnHandShakeStart doesn't get a ServerVersion string when event is being invoked on client.

<br>
<h3>Available events:</h3>

```C#
NetworkEvents.Listener.ClientConnected += OnClientConnected;
NetworkEvents.Listener.ClientDisconnect += OnClientDisconnect;
NetworkEvents.Listener.ServerShutdown += OnServerShutdown;
NetworkEvents.Listener.ServerStart += OnServerStart;
NetworkEvents.Listener.MessageSent += OnMessageSent;
NetworkEvents.Listener.MessageReceived += OnMessageReceived;
NetworkEvents.Listener.HandshakeStart += OnHandShakeStart;
NetworkEvents.Listener.HandshakeEnd += OnHandShakeEnd;
```

```C#
public static void OnClientConnected(object sender, OnClientConnectEvent eventData){
    Console.WriteLine($"*EVENT* CLIENT CONNECTED! ({eventData.UserName} ID:{eventData.ClientID} SUCCESS:{eventData.Success})");
}
public static void OnClientDisconnect(object sender, OnClientDisconnectEvent eventData){
    Console.WriteLine($"*EVENT* CLIENT DISCONNECTED! ({eventData.UserName} ID:{eventData.ClientID} SUCCESS:{eventData.Success})");
}
public static void OnServerStart(object sender, OnServerStartEvent eventData){
    Console.WriteLine($"*EVENT* SERVER STARTED! SUCCESS:{eventData.Success}");
}
public static void OnServerShutdown(object sender, OnServerShutdownEvent eventData){
    Console.WriteLine($"*EVENT* SERVER STOPPED! SUCCESS:{eventData.Success}");
}
public static void OnMessageSent(object sender, OnMessageSentEvent eventData){
    Console.WriteLine($"*EVENT* MSG SENT: {eventData.Message?.MethodName}");
}
public static void OnMessageReceived(object sender, OnMessageReceivedEvent eventData){
    Console.WriteLine($"*EVENT* MSG RECEIVED: {eventData.Message?.MethodName}");
}
public static void OnHandShakeStart(object sender, OnHandShakeStartEvent eventData){
    Console.WriteLine($"*EVENT* HANDSHAKE STARTED: version:{eventData.ClientVersion}, username:{eventData.UserName}");
}
public static void OnHandShakeEnd(object sender, OnHandShakeEndEvent eventData){
    Console.WriteLine($"*EVENT* HANDSHAKE ENDED: Success:{eventData.ServerVersion}, Code:{eventData.ErrorCode}");
}
```

<h3>When using client framework you can test if the event was invoked for self connect using this:</h3>

```C#
public static void OnClientConnected(object sender, OnClientConnectEvent eventData){
    if (eventData.ClientID == Network.Client.ID) {
        Console.WriteLine($"*EVENT* YOU CONNECTED! ({eventData.UserName} ID:{eventData.ClientID} SUCCESS:{eventData.Success})");
        return;
    }
    Console.WriteLine($"*EVENT* CLIENT CONNECTED! ({eventData.UserName} ID:{eventData.ClientID})");
}
public static void OnClientDisconnected(object sender, OnClientDisconnectEvent eventData){
    if (eventData.ClientID == Network.Client.ID) {
        Console.WriteLine($"*EVENT* YOU DISCONNECTED! ({eventData.UserName} ID:{eventData.ClientID} SUCCESS:{eventData.Success})");
        return;
    }
    Console.WriteLine($"*EVENT* CLIENT DISCONNECTED! ({eventData.UserName} ID:{eventData.ClientID} SUCCESS:{eventData.Success})");
}
```
<br>
<h2>Recompile</h2>
Clone this project to your local PC.
To recompile the project to classlib from the current console version use the <code>buildDLL.sh</code> file!
This will create folder that has the file version as its name inside the <code>buildDLL.sh</code>  directory.