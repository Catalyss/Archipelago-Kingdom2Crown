using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
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
                var host = address.Split(':')[0].Replace(":", "");
                var port = int.Parse(address.Split(':')[1].Replace(":", ""));

                Plugin.Log.LogInfo($"{host} , {port} , {slotName} , {password}");

                Session = ArchipelagoSessionFactory.CreateSession(host, port);
                Session.Items.ItemReceived += OnItemReceived;
                Session.Socket.PacketReceived += OnPacktRevieved;

                LoginResult result = await Task.Run(() => Session.TryConnectAndLogin(
                    "Kingdom Two Crown",     // game name (must match your server-side name)
                    slotName,
                    ItemsHandlingFlags.IncludeOwnItems | ItemsHandlingFlags.IncludeStartingInventory,
                    new Version(0, 6, 1),
                    new string[] { "AP", "DeathLink" },
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

        private static async void OnPacktRevieved(ArchipelagoPacketBase packet)
        {

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
