


<h1>Server-client-framework</h1>

This framework is divided to two different namespaces. One for server framework and another for CLIENT framework.

<h2>Setup using DLL reference</h2>
To get started create new folder project using:
<code>dotnet new console</code>
Once completed add reference

<h2>Setup using NuGet package</h2>
TODO
<code></code>

<h2>Server Framework</h2>

<code>
using ServerFramework;
Network.StartServer();
</code>


<h2>Client Framework</h2>

<code>
using ClientFramework;
Network.Connect();
</code>

<h2>Recompile</h2>
Clone this project to your local PC.
To recompile the project to classlib from the current console version use the pack.sh file!