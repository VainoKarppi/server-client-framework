


<h1>Server-client-framework</h1>

This framework is divided to two different namespaces. One for <u>SERVER</u> framework and another for <u>CLIENT</u> framework.

<br>
<h2>Setup using DLL reference</h2>
To get started create new folder project using:<br>

<code>dotnet new console -n MyServerProject</code>

Next recompile the project using ```buildDLL.sh``` or <u>DOWNLOAD</u> one from:<h3>[HERE](https://github.com/Razer223/server-client-framework/releases/tag/test)</h3>

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



<h2>Setup using NuGet package</h2>
TODO
<code></code>

<br>
<h2>Server Framework</h2>

```C#
using ServerFramework;

Network.StartServer();

Network.RegisterMethod( typeof(ClientMethods) ); // SEE EXAMPLE BELOW!
```

<br>
<h2>Client Framework</h2>

```C#
using ClientFramework;

Network.Connect();

Network.RegisterMethod( typeof(ClientMethods) ); // SEE EXAMPLE BELOW!
```

<br>
<h2>Events</h2>
Events are executed on both server and/or client. Some events might differ depending where its being executed at. For example OnHandShakeStart doesent get ServerVersion string when event is being invoked on client.

<br>
<h2>Recompile</h2>
Clone this project to your local PC.
To recompile the project to classlib from the current console version use the pack.sh file!