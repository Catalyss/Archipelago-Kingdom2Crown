using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using System;
using System.Threading.Tasks;


namespace APKingdom2Crown.Archipelago
{
    public class APConnection
    {
        public ArchipelagoSession Session { get; private set; }
        public bool Connected => Session?.Socket?.Connected ?? false;

        public async Task<bool> Connect(string address, string slotName, string password = "")
        {
            try
            {
                if (!Connected)
                {
                    Plugin.Log.LogInfo($"{address} , {slotName} , {password}");

                    Session = ArchipelagoSessionFactory.CreateSession($"{address}");
                    Session.Socket.SocketClosed += SocketClosed;
                    Session.Socket.ErrorReceived += ErrorReceived;
                    Session.Items.ItemReceived += OnItemReceived;
                    Session.Socket.PacketReceived += OnPacktRevieved;

                    Plugin.Log.LogInfo($"Caugh a Session = {Session != null}");
                }
                //the Connect trigger OperationCanceledException causing a "Time out"
                LoginResult result = await Task.Run(() => Session.TryConnectAndLogin(
                    "Kingdom Two Crowns",     // game name (must match your server-side name)
                    slotName,
                    ItemsHandlingFlags.AllItems,
                    new Version("0.6.1"),
                    new string[] { "DeathLink", "NoText" },
                    null,
                    password,
                    true
                ));

                if (result is LoginSuccessful success)
                {
                    Plugin.Log.LogInfo($"[AP] Connected as {success.Slot} in team {success.Team}");
                    return true;
                }
                else
                {
                    var failure = (LoginFailure)result;
                    Session = null;
                    Plugin.Log.LogError($"[AP] Login failed: {string.Join(", ", failure.Errors)}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[AP] Connection failed: {ex}");
                return false;
            }
        }

        public static void SocketClosed(string reason)
        {
            Console.WriteLine("WS CLOSED");
            Console.WriteLine("Lost connection to Archipelago server. " + reason);
        }

        public static void ErrorReceived(Exception e, string message)
        {
            Console.WriteLine("WS ERRORED");
            Console.WriteLine(message);
            if (e != null) Console.WriteLine(e.ToString());
            //Disconnect();
        }

        private static async void OnPacktRevieved(ArchipelagoPacketBase packet)
        {
            Plugin.Log.LogInfo(packet.ToJObject().ToString());
        }

        private static async void OnItemReceived(ReceivedItemsHelper helper)
        {
            foreach (var item in helper.AllItemsReceived)
            {
                Plugin.Log.LogInfo($"[AP] Received item: {item.ItemGame} from {item.Player}");
                helper.DequeueItem();
            }
        }

        public void CompleteLocation(long locationId)
        {
            if (!Connected) return;

            Plugin.Log.LogInfo($"[AP] Completed location {locationId}");
            Session.Locations.CompleteLocationChecks(locationId);
        }
    }
}
