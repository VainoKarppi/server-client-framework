using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace EdenOnlineExtensionServer {
    class ClientFunctions {
        public static string Test(NetworkClient client, object[] parameters, byte RequestType) {
            return "te22st from server!";
        }
        public static void Disconnect(NetworkClient client, object[] parameters, byte RequestType) {
            throw new NotImplementedException();
        }

        public static ArmaArray TestArray(NetworkClient client, object[] parameters, byte RequestType) {
            foreach (var x in parameters) Log.Write(x);
            return new ArmaArray {"TestArray",121};
        }
        
        public static short Handshake(NetworkClient client, object[] parameters, byte RequestType) {

            // RETURNS client id if success (minus number if error (each value is one type of error))
            int _clientVersion = Int32.Parse((string)parameters[0]);
            string _userName = (string)parameters[1];
            string _worldName = (string)parameters[2];
            //string[] _mods = (string[])parameters[3];
            string[] _mods = new string[] {}; //TODO

            client.UserName = _userName;

            int serverVersion = Program.Version;

            //TODO add major and minor checking
            if (_clientVersion != serverVersion) {
                Log.Write($"User {_userName} has wrong version! Should be: {serverVersion} has: {_clientVersion}");
                return -1;
            }

            if (!_worldName.ToLower().Equals(Network.WorldName.ToLower())) {
                Log.Write($"User {_userName} has wrong map loaded! Should be: {Network.WorldName}, has: {_worldName}");
                return -2;
            }
            
            foreach (string serverMod in Network.Mods) {
                if (!_mods.Contains(serverMod, StringComparer.CurrentCultureIgnoreCase)) {
                    Log.Write($"User {_userName} is missing mod: {serverMod}!");
                    return -3;
                }   
            }

            Log.Write($"*SUCCESS* {client.ID} Handshake completed!");

            return client.ID;
        }
        public static string ConnectedClients(NetworkClient client, object[] parameters, byte RequestType) {
            // TODO send as list or array (ADD IN FRAMEWORK)
            StringBuilder users = new StringBuilder();
            foreach (short _clientID in Network.ClientList.Keys) {
                users.Append(_clientID.ToString());
                users.Append(",");
            }

            return users.ToString();

        }
    }
}
