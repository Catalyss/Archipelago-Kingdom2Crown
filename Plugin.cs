using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Coatsink.Common;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Runtime;
using JetBrains.Annotations;
using LibCpp2IL.Elf;
using Newtonsoft.Json;
using PlayFab.Internal;
using TMPro;
using UnityEngine; // For Input
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static APKingdom2Crown.KeyHandler;

//344 x 160
//1920 x 1080

// AP STARTED ID OFFSET START 755067

namespace APKingdom2Crown;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static bool APconnected = false;
    internal static new ManualLogSource Log;
    internal static Archipelago.APConnection AP;

    public static ConfigEntry<string> ConfigHost;
    public static ConfigEntry<string> ConfigSlot;
    public static ConfigEntry<string> ConfigPassword;
    public static ConfigEntry<int> ConfigDeathLinkMode;

    public static Plugin Instance { get; private set; }

    public override void Load()
    {
        Log = base.Log;
        Log.LogInfo("Loading Archipelago integration...");
        Instance = this;

        ConfigHost = Config.Bind("Archipelago", "Host", "archipelago.gg:38281",
    "The address and port of the Archipelago server.");

        ConfigSlot = Config.Bind("Archipelago", "Slot", "Monarch",
            "Your Archipelago slot name.");

        ConfigPassword = Config.Bind("Archipelago", "Password", "",
            "Your Archipelago game password.");

        ConfigDeathLinkMode = Config.Bind("Archipelago", "DeathLinkMode", 0,
            "Death Link Mode: 0=None, 1=Easy, 2=Hard");

        TouchTypes();

        ClassInjector.RegisterTypeInIl2Cpp<ProgressUpdateChecks>();
        ClassInjector.RegisterTypeInIl2Cpp<StatueBlocker>();
        ClassInjector.RegisterTypeInIl2Cpp<ArchipelagoPauseUI>();
        ClassInjector.RegisterTypeInIl2Cpp<InGameLogger>();


        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();

        APIDsRegisterClass.RegisterAllIds();

        AddComponent<KeyHandler>();

        AP = new Archipelago.APConnection();

        Log.LogInfo("Archipelago Kingdom Two Crowns mod Fully initialized!");
    }
    public static void OpenConfig()
    {
        System.Diagnostics.Process.Start("notepad.exe", Instance.Config.ConfigFilePath);
    }
    public static void ReloadConfig()
    {
        Instance.Config.Reload();
    }

    public static void TouchTypes()
    {
        _ = typeof(ProgressUpdateChecks);
        _ = typeof(StatueBlocker);
        _ = typeof(ArchipelagoPauseUI);
        _ = typeof(InGameLogger);
    }

}



[HarmonyPatch(typeof(SteedSpawn), "Pay")]
public class SteedPayPatch
{
    static readonly string[] SteedUnlockNames =
    {
        "Bear Unlocks",
        "Stag Unlocks",
        "Lizard Unlocks",
        "Unicorn Unlocks",
        "Wolf P1 Unlocks",
        "Warhorse P1 Unlocks",
        "Horse Fast Unlocks",
        "Griffin P1 Unlocks",
        "Griffin Skull P1 Unlocks",
        "Horse Stamina Unlocks",
        "Wolf_norselands Unlocks",
        "Reindeer Unlocks",
        "Horse Burst Unlocks",
        "Rainbow Pony Unlocks",
        "Warhorse Plague P1 Unlocks",
        "Wolf Norselands P1 Unlocks",
        "Unicorn Dark Unlocks"
    };

    static void Postfix(SteedSpawn __instance)
    {
        if (!Plugin.APconnected) return;
        foreach (var steed in __instance.steeds)
        {
            steed.forceBlockPayment = true;

            foreach (var unlockName in SteedUnlockNames)
            {
                if (steed.name.Contains(unlockName.Replace(" Unlocks", ""), StringComparison.OrdinalIgnoreCase))
                {
                    var id = APIDRegistry.Get(unlockName);
                    if (id != -1)
                    {
                        Plugin.AP?.CompleteLocation(id);
                        Plugin.Log.LogInfo($"[AP] Steed '{steed.name}' → Completed location '{unlockName}' (ID {id})");
                    }
                    else
                    {
                        Plugin.Log.LogWarning($"[AP] No registry entry found for '{unlockName}'");
                    }
                    break;
                }
            }
        }
    }
}

[HarmonyPatch(typeof(Chest), "ReceiveOpenCall")]
public class ChestOpenPatch
{
    static void Postfix(Chest __instance)
    {
        if (!Plugin.APconnected) return;
        if (__instance.currencyType == CurrencyType.Coins)
        {
            APHelper.SendNextProgressiveCheck("Coin Chest Progressive", 8);
        }
        else if (__instance.currencyType == CurrencyType.Gems)
        {
            APHelper.SendNextProgressiveCheck("Gem Chest Progressive", 12);
        }
    }
}

[HarmonyPatch(typeof(Portal), "OnDestroy")]
public class PortalOnDestroyPatch
{
    static void Prefix(Portal __instance)
    {
        if (!Plugin.APconnected) return;
        if (__instance.type == Portal.Type.Cliff) return;

        Plugin.Log.LogInfo("[AP] Portal destroyed → Checking progressive sequence...");
        APHelper.SendNextProgressiveCheck("Destroy Portal Progressive", 23);
    }
}

[HarmonyPatch(typeof(Steed), "Update")]
public class SteedUpdatePatch
{
    static readonly string[] SteedUnlockNames =
    {
        "Bear Use",
        "Stag Use",
        "Lizard Use",
        "Unicorn Use",
        "Wolf P1 Use",
        "Warhorse P1 Use",
        "Horse Fast Use",
        "Griffin P1 Use",
        "Griffin Skull P1 Use",
        "Horse Stamina Use",
        "Wolf_norselands Use",
        "Reindeer Use",
        "Horse Burst Use",
        "Rainbow Pony Use",
        "Warhorse Plague P1 Use",
        "Wolf Norselands P1 Use",
        "Unicorn Dark Use"
    };

    static void Postfix(Steed __instance)
    {
        if (!Plugin.APconnected) return;
        foreach (var unlockName in SteedUnlockNames)
        {
            if (__instance.name.Contains(unlockName.Replace(" Use", ""), StringComparison.OrdinalIgnoreCase))
            {
                __instance.forceBlockPayment = !APItemRegistry.IsCollected(APIDRegistry.Get(unlockName));
                break;
            }
        }
    }

}

[HarmonyPatch(typeof(Hermit), "Update")]
public class HermitUpdatePatch
{
    static void Postfix(Hermit __instance)
    {
        if (!Plugin.APconnected) return;
        switch (__instance.Type)
        {
            case Hermit.HermitType.Ballista:
                __instance.forceBlockPayment = !APItemRegistry.IsCollected(APIDRegistry.Get("Hermit Ballista Use"));
                break;
            case Hermit.HermitType.Baker:
                __instance.forceBlockPayment = !APItemRegistry.IsCollected(APIDRegistry.Get("Hermit Bakery Use"));
                break;
            case Hermit.HermitType.Horn:
                __instance.forceBlockPayment = !APItemRegistry.IsCollected(APIDRegistry.Get("Hermit Horn Use"));
                break;
            case Hermit.HermitType.Knight:
                __instance.forceBlockPayment = !APItemRegistry.IsCollected(APIDRegistry.Get("Hermit Warrior Use"));
                break;
            case Hermit.HermitType.Horse:
                __instance.forceBlockPayment = !APItemRegistry.IsCollected(APIDRegistry.Get("Hermit Stable Use"));
                break;
        }
    }
}

[HarmonyPatch(typeof(Cabin), "Pay")]
public class CabinOnPayPatch
{
    static void Prefix(Cabin __instance)
    {
        if (!Plugin.APconnected) return;
        __instance.hermitPrefab.forceBlockPayment = true;

        switch (__instance.hermitType)
        {
            case Hermit.HermitType.Ballista:
                Plugin.AP?.CompleteLocation(APIDRegistry.Get("Hermit Ballista Unlock"));
                break;
            case Hermit.HermitType.Baker:
                Plugin.AP?.CompleteLocation(APIDRegistry.Get("Hermit Bakery Unlock"));
                break;
            case Hermit.HermitType.Horn:
                Plugin.AP?.CompleteLocation(APIDRegistry.Get("Hermit Horn Unlock"));
                break;
            case Hermit.HermitType.Knight:
                Plugin.AP?.CompleteLocation(APIDRegistry.Get("Hermit Warrior Unlock"));
                break;
            case Hermit.HermitType.Horse:
                Plugin.AP?.CompleteLocation(APIDRegistry.Get("Hermit Stable Unlock"));
                break;
        }
        Plugin.Log.LogInfo($"[AP] Hermit Brough → Sending Check for Location ");
    }
}

[HarmonyPatch(typeof(Statue), "Awake")]
public class StatueAwakePatch
{
    static void Postfix(Statue __instance)
    {
        var go = __instance.gameObject;

        if (go.GetComponent(Il2CppType.Of<StatueBlocker>()) == null)
        {
            go.AddComponent(Il2CppType.Of<StatueBlocker>());
            Plugin.Log.LogInfo($"[AP] StatueBlocker added to {__instance.deity}");
        }
    }
}

[HarmonyPatch(typeof(Statue), "Pay")]
public class StatueOnPayPatch
{
    static void Prefix(Statue __instance)
    {
        if (!Plugin.APconnected) return;
        if (__instance.deityStatus == Statue.DeityStatus.GemLocked)
        {
            switch (__instance.deity)
            {
                case Statue.Deity.Archer:
                    Plugin.AP?.CompleteLocation(APIDRegistry.Get("Archery Unlocks"));
                    break;
                case Statue.Deity.Worker:
                    Plugin.AP?.CompleteLocation(APIDRegistry.Get("Building Unlocks"));
                    break;
                case Statue.Deity.Knight:
                    Plugin.AP?.CompleteLocation(APIDRegistry.Get("Knights Unlocks"));
                    break;
                case Statue.Deity.Farmer:
                    Plugin.AP?.CompleteLocation(APIDRegistry.Get("Scythe Unlocks"));
                    break;
            }
        }
        Plugin.Log.LogInfo($"[AP] Hermit Brough → Sending Check for Location ");
    }
}

[HarmonyPatch(typeof(Kingdom), "Update")]
public class KingdomUpdatePatch
{
    static void Prefix(Kingdom __instance)
    {
        if (!Plugin.APconnected) return;
        __instance.StoneBuildingUnlocked = APItemRegistry.IsCollected(APIDRegistry.Get("Stone Technology"));
        __instance.IronBuildingUnlocked = APItemRegistry.IsCollected(APIDRegistry.Get("Iron Technology"));

        // Handle campfire when castle not yet built
        if (!__instance.castle)
        {
            var campfireUpgrade = __instance.campfire?.GetComponent<PayableUpgrade>();
            if (campfireUpgrade != null)
                campfireUpgrade.forceBlockPayment = false;
            return;
        }

        // Get the upgrade component once
        var upgrade = __instance.castle.GetComponent<PayableUpgrade>();
        if (upgrade == null)
            return;

        string unlockName = __instance.castle.level switch
        {
            Castle.Level.Castle1 => "Progressive Castle Unlock T1",
            Castle.Level.Castle2 => "Progressive Castle Unlock T2",
            Castle.Level.Castle3 => "Progressive Castle Unlock T3",
            Castle.Level.Castle4 => "Progressive Castle Unlock T4",
            Castle.Level.Castle5 => "Progressive Castle Unlock T5",
            Castle.Level.Castle6 => "Progressive Castle Unlock T6",
            Castle.Level.Castle7 => "Progressive Castle Unlock T7",
            _ => null
        };

        if (unlockName != null)
            upgrade.forceBlockPayment = !APItemRegistry.IsCollected(APIDRegistry.Get(unlockName));
    }
}

[HarmonyPatch(typeof(ProgressHelper), "Awake")]
public class ProgressHelperAwakePatch
{
    static void Postfix(ProgressHelper __instance)
    {
        var go = __instance.gameObject;

        // Check if component exists
        if (go.GetComponent(Il2CppType.Of<ProgressUpdateChecks>()) == null)
        {
            go.AddComponent(Il2CppType.Of<ProgressUpdateChecks>());
            Plugin.Log.LogInfo("[AP] ProgressUpDateChecks added to ProgressHelper");
        }
    }
}

[HarmonyPatch(typeof(ScreenManager), "Awake")]
public class ScreenManagerAwakePatch
{
    static void Postfix(ScreenManager __instance)
    {
        if (PluginUI.UILogCanvas == null) PluginUI.SetupUI();

        var go = __instance.transform.GetChild(5).gameObject;

        // Check if component exists
        if (go.GetComponent(Il2CppType.Of<ArchipelagoPauseUI>()) == null)
        {
            go.AddComponent(Il2CppType.Of<ArchipelagoPauseUI>());
            Plugin.Log.LogInfo("[AP] APConnectionUI added to ScreenManager → Pause Menue");
        }
    }
}

[HarmonyPatch(typeof(DogSpawn), "FreeDog")]
public class DogSpawnFreeDogPatch
{
    static void Prefix(DogSpawn __instance)
    {
        if (!Plugin.APconnected) return;
        Plugin.AP?.CompleteLocation(APIDRegistry.Get("Hermit Ballista Unlock"));
        Plugin.Log.LogInfo($"[AP] Dog acquired  → Island 2 Dog");
    }
}

[HarmonyPatch(typeof(Player), "LoseCrown")]
public class PlayerLoseCrownPatch
{
    static void Postfix(Player __instance)
    {
        if (!Plugin.APconnected) return;
        //TODO Death Link Packet
        Plugin.AP?.Session.Socket.SendPacket(null);
        Plugin.Log.LogInfo($"[AP] Dog acquired  → Island 2 Dog");
    }
}

public static class APHelper
{
    public static int? GetNextUncheckedLocation(string baseName, int count)
    {
        if (Plugin.AP?.Session?.Locations.AllLocationsChecked == null)
        {
            Plugin.Log.LogWarning("[AP] Session or location data not ready.");
            return null;
        }

        var checkedLocations = Plugin.AP.Session.Locations.AllLocationsChecked;

        for (int i = 1; i <= count; i++)
        {
            string locName = $"{baseName} {i}";
            var id = APIDRegistry.Get(locName);
            if (id == -1)
            {
                Plugin.Log.LogWarning($"[AP] Unknown location name: {locName}");
                continue;
            }

            if (!checkedLocations.Contains(id))
            {
                Plugin.Log.LogInfo($"[AP] Next unchecked location found: {locName} ({id})");
                return id;
            }
        }

        Plugin.Log.LogInfo($"[AP] All locations for {baseName} already checked.");
        return null;
    }

    public static void SendNextProgressiveCheck(string baseName, int totalCount)
    {
        int? next = GetNextUncheckedLocation(baseName, totalCount);
        if (next == null)
        {
            Plugin.Log.LogInfo($"[AP] No unchecked progressive location for {baseName}.");
            return;
        }

        Plugin.Log.LogInfo($"[AP] Sending check for progressive location: {baseName} ({next})");
        Plugin.AP?.CompleteLocation((int)next);
    }
}
public static class APItemRegistry
{
    // Stores all item IDs and their collected status
    private static readonly Dictionary<int, bool> _items = new();

    // Add an item ID to the registry
    public static void RegisterItem(int itemId)
    {
        if (!_items.ContainsKey(itemId))
            _items[itemId] = Plugin.AP?.Session?.Items.AllItemsReceived?.Select(s => s.ItemId == itemId) != null;
    }

    // Mark an item as collected
    public static void CollectItem(int itemId)
    {
        if (_items.ContainsKey(itemId))
            _items[itemId] = true;
    }

    // Check if an item has been collected
    public static bool IsCollected(int itemId)
    {
        return _items.TryGetValue(itemId, out var collected) && collected;
    }

    // Optional: get all collected items
    public static IEnumerable<int> GetCollectedItems()
    {
        foreach (var kvp in _items)
            if (kvp.Value) yield return kvp.Key;
    }

    // Optional: initialize a batch of items
    public static void RegisterItems(IEnumerable<int> itemIds)
    {
        foreach (var id in itemIds)
            RegisterItem(id);
    }
}
public static class APIDRegistry
{
    private const int BASE_ID = 755067;
    private static int _nextId = BASE_ID;
    private static readonly Dictionary<string, int> _ids = new();

    /// <summary>
    /// Register a name → ID mapping.
    /// </summary>
    public static void AddNew(string name, int? id = null)
    {
        if (_ids.ContainsKey(name))
        {
            Plugin.Log.LogWarning($"[AP] Duplicate ID registration skipped for '{name}'");
            return;
        }

        int finalId = id ?? _nextId++;
        _ids[name] = finalId;
        Plugin.Log.LogInfo($"[AP] Registered '{name}' → {finalId}");
    }

    /// <summary>
    /// Retrieve a registered ID by name.
    /// </summary>
    public static int Get(string name)
    {
        if (_ids.TryGetValue(name, out int id))
            return id;

        Plugin.Log.LogError($"[AP] No ID found for '{name}'");
        return -1;
    }

    /// <summary>
    /// Convenience: get all registered IDs.
    /// </summary>
    public static IReadOnlyDictionary<string, int> All => _ids;
}
public static class APIDsRegisterClass
{
    public static void RegisterAllIds()
    {
        // --- Progressive Portals ---
        APIDRegistry.AddNew("Destroy Portal Progressive 1", 755067);            //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 2", 755068);            //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 3", 755069);            //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 4", 755070);            //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 5", 755071);            //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 6", 755072);            //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 7", 755073);            //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 8", 755074);            //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 9", 755075);            //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 10", 755076);           //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 11", 755077);           //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 12", 755078);           //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 13", 755079);           //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 14", 755080);           //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 15", 755081);           //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 16", 755082);           //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 17", 755083);           //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 18", 755084);           //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 19", 755085);           //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 20", 755086);           //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 21", 755087);           //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 22", 755088);           //Location  1
        APIDRegistry.AddNew("Destroy Portal Progressive 23", 755089);           //Location  1

        // --- Progressive Castle Unlock ---                        
        APIDRegistry.AddNew("Progressive Castle Unlock T1", 755091);            //Item      1
        APIDRegistry.AddNew("Progressive Castle Unlock T2", 755092);            //Item      1
        APIDRegistry.AddNew("Progressive Castle Unlock T3", 755093);            //Item      1 
        APIDRegistry.AddNew("Progressive Castle Unlock T4", 755094);            //Item      1 
        APIDRegistry.AddNew("Progressive Castle Unlock T5", 755095);  //Stone   //Item      1 
        APIDRegistry.AddNew("Progressive Castle Unlock T6", 755177);  //Stone   //Item      1 
        APIDRegistry.AddNew("Progressive Castle Unlock T7", 755178);  //Iron    //Item      1 

        // --- Hermits ---
        APIDRegistry.AddNew("Hermit Ballista Unlock", 755096);                  //Location  1
        APIDRegistry.AddNew("Hermit Ballista Use", 755097);                     //Item      1
        APIDRegistry.AddNew("Hermit Bakery Unlock", 755098);                    //Location  1
        APIDRegistry.AddNew("Hermit Bakery Use", 755099);                       //Item      1
        APIDRegistry.AddNew("Hermit Horn Unlock", 755100);                      //Location  1
        APIDRegistry.AddNew("Hermit Horn Use", 755101);                         //Item      1
        APIDRegistry.AddNew("Hermit Stable Unlock", 755102);                    //Location  1
        APIDRegistry.AddNew("Hermit Stable Use", 755103);                       //Item      1
        APIDRegistry.AddNew("Hermit Warrior Unlock", 755104);                   //Location  1
        APIDRegistry.AddNew("Hermit Warrior Use", 755105);                      //Item      1

        // --- Island 1 ---
        APIDRegistry.AddNew("Island 1 Liberation", 755106);                     //Location  1
        APIDRegistry.AddNew("Progressive Coin Chest 1", 755107);                //Location  1   
        APIDRegistry.AddNew("Progressive Coin Chest 2", 755108);                //Location  1   
        APIDRegistry.AddNew("Progressive Coin Chest 3", 755109);                //Location  1   

        // --- Island 2 ---                     
        APIDRegistry.AddNew("Island 2 Liberation", 755110);                     //Location  1
        APIDRegistry.AddNew("Progressive Coin Chest 4", 755111);                //Location  1   
        APIDRegistry.AddNew("Progressive Coin Chest 5", 755112);                //Location  1   
        APIDRegistry.AddNew("Progressive Coin Gem 1", 755113);                  //Location  1
        APIDRegistry.AddNew("Progressive Coin Gem 2", 755114);                  //Location  1
        APIDRegistry.AddNew("Progressive Coin Gem 3", 755115);                  //Location  1
        APIDRegistry.AddNew("Island 2 Dog", 755116);                            //Location

        // --- Island 3 ---                     
        APIDRegistry.AddNew("Island 3 Liberation", 755117);                     //Location
        APIDRegistry.AddNew("Progressive Coin Chest 6", 755118);                //Location  1   
        APIDRegistry.AddNew("Progressive Coin Chest 7", 755119);                //Location  1   
        APIDRegistry.AddNew("Progressive Coin Chest 8", 755120);                //Location  1   
        APIDRegistry.AddNew("Progressive Coin Gem 4", 755121);                  //Location  1
        APIDRegistry.AddNew("Progressive Coin Gem 5", 755122);                  //Location  1
        APIDRegistry.AddNew("Progressive Coin Gem 6", 755123);                  //Location  1

        // --- Island 4 ---                     
        APIDRegistry.AddNew("Island 4 Liberation", 755124);                     //Location
        APIDRegistry.AddNew("Progressive Coin Chest 9", 755125);                //Location  1   
        APIDRegistry.AddNew("Progressive Coin Gem 7", 755126);                  //Location  1
        APIDRegistry.AddNew("Progressive Coin Gem 8", 755127);                  //Location  1
        APIDRegistry.AddNew("Progressive Coin Gem 9", 755128);                  //Location  1

        // --- Island 5 ---                     
        APIDRegistry.AddNew("Island 5 Liberation", 755129);                     //Location  1
        APIDRegistry.AddNew("Progressive Coin Gem 10", 755130);                 //Location  1
        APIDRegistry.AddNew("Progressive Coin Gem 11", 755131);                 //Location  1
        APIDRegistry.AddNew("Progressive Coin Gem 12", 755132);                 //Location  1

        // --- Mount ---
        APIDRegistry.AddNew("Bear Unlocks", 755133);                            //Location  1
        APIDRegistry.AddNew("Stag Unlocks", 755134);                            //Location  1
        APIDRegistry.AddNew("Lizard Unlocks", 755135);                          //Location  1
        APIDRegistry.AddNew("Unicorn Unlocks", 755136);                         //Location  1
        APIDRegistry.AddNew("Wolf P1 Unlocks", 755137);                         //Location  1
        APIDRegistry.AddNew("Warhorse P1 Unlocks", 755138);                     //Location  1
        APIDRegistry.AddNew("Horse Fast Unlocks", 755139);                      //Location  1
        APIDRegistry.AddNew("Griffin P1 Unlocks", 755140);                      //Location  1
        APIDRegistry.AddNew("Griffin Skull P1 Unlocks", 755141);                //Location  1
        APIDRegistry.AddNew("Horse Stamina Unlocks", 755142);                   //Location  1
        APIDRegistry.AddNew("Wolf_norselands Unlocks", 755143);                 //Location  1
        APIDRegistry.AddNew("Reindeer Unlocks", 755144);                        //Location  1
        APIDRegistry.AddNew("Horse Burst Unlocks", 755145);                     //Location  1
        APIDRegistry.AddNew("Rainbow Pony Unlocks", 755146);                    //Location  1
        APIDRegistry.AddNew("Warhorse Plague P1 Unlocks", 755147);              //Location  1  
        APIDRegistry.AddNew("Wolf Norselands P1 Unlocks", 755148);              //Location  1  
        APIDRegistry.AddNew("Unicorn Dark Unlocks", 755149);                    //Location  1

        APIDRegistry.AddNew("Bear Use", 755150);                                //Items     1
        APIDRegistry.AddNew("Stag Use", 755151);                                //Items     1
        APIDRegistry.AddNew("Lizard Use", 755152);                              //Items     1
        APIDRegistry.AddNew("Unicorn Use", 755153);                             //Items     1
        APIDRegistry.AddNew("Wolf P1 Use", 755154);                             //Items     1
        APIDRegistry.AddNew("Warhorse P1 Use", 755155);                         //Items     1
        APIDRegistry.AddNew("Horse Fast Use", 755156);                          //Items     1
        APIDRegistry.AddNew("Griffin P1 Use", 755157);                          //Items     1
        APIDRegistry.AddNew("Griffin Skull P1 Use", 755158);                    //Items     1
        APIDRegistry.AddNew("Horse Stamina Use", 755159);                       //Items     1
        APIDRegistry.AddNew("Wolf_norselands Use", 755160);                     //Items     1
        APIDRegistry.AddNew("Reindeer Use", 755161);                            //Items     1
        APIDRegistry.AddNew("Horse Burst Use", 755162);                         //Items     1
        APIDRegistry.AddNew("Rainbow Pony Use", 755163);                        //Items     1
        APIDRegistry.AddNew("Warhorse Plague P1 Use", 755164);                  //Items     1
        APIDRegistry.AddNew("Wolf Norselands P1 Use", 755165);                  //Items     1
        APIDRegistry.AddNew("Unicorn Dark Use", 755166);                        //Items     1

        // --- Status ---
        APIDRegistry.AddNew("Archery Use", 755167);                             //Items     1
        APIDRegistry.AddNew("Scythe Use", 755168);                              //Items     1
        APIDRegistry.AddNew("Building Use", 755169);                            //Items     1
        APIDRegistry.AddNew("Knights Use", 755170);                             //Items     1

        APIDRegistry.AddNew("Archery Unlocks", 755171);                         //Location  1
        APIDRegistry.AddNew("Scythe Unlocks", 755172);                          //Location  1
        APIDRegistry.AddNew("Building Unlocks", 755173);                        //Location  1
        APIDRegistry.AddNew("Knights Unlocks", 755174);                         //Location  1

        // --- Tech Use ---
        APIDRegistry.AddNew("Stone Technology", 755175);                        //Item      1  
        APIDRegistry.AddNew("Iron Technology", 7551176);                        //Item      1

        APIDRegistry.AddNew("Random Game Effect (junk)", 755177);               //Junk

        int[] hermitUseIds = { 755097, 755099, 755101, 755103, 755105 };
        int[] mountUseIds = { 755150, 755151, 755152, 755153, 755154, 755155, 755156, 755157, 755158, 755159, 755160, 755161, 755162, 755163, 755164, 755165, 755166 };
        int[] statusUseIds = { 755167, 755168, 755169, 755170 };
        int[] techUseIds = { 755175, 755176 };
        int[] castleUseIds = { 755091, 755092, 755093, 755094, 755095 };
        int[] junkIds = { 755177 };

        APItemRegistry.RegisterItems(hermitUseIds);
        APItemRegistry.RegisterItems(mountUseIds);
        APItemRegistry.RegisterItems(statusUseIds);
        APItemRegistry.RegisterItems(techUseIds);
        APItemRegistry.RegisterItems(castleUseIds);
        APItemRegistry.RegisterItems(junkIds);
    }

    public static int GetID(string name) => APIDRegistry.Get(name);
}
public static class PluginUI
{
    public static Canvas UILogCanvas;
    public static TMP_FontAsset KingdomTMPFont;
    public static InGameLogger Logger;

    public static void SetupUI()
    {
        // Create Canvas
        if (UILogCanvas == null)
        {
            GameObject canvasGO = new GameObject("AP_UI_Canvas");
            UILogCanvas = canvasGO.AddComponent<Canvas>();
            UILogCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            UnityEngine.Object.DontDestroyOnLoad(canvasGO);
        }

        if (KingdomTMPFont != null) return;

        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (var font in fonts)
        {
            if (UnityEngine.Object.GetName(font).Contains("Kingdom"))
            {
                KingdomTMPFont = font;
                Plugin.Log.LogInfo("[AP] Kingdom_TMP font found!");
                break;
            }
        }

        if (KingdomTMPFont == null)
            Plugin.Log.LogWarning("[AP] Kingdom_TMP font not found, default font will be used!");

        // Add InGameLogger
        if (Logger == null)
        {
            GameObject go = new GameObject("AP_InGameLogger");
            go.transform.SetParent(UILogCanvas.transform, false);
            Logger = go.AddComponent<InGameLogger>();
        }
    }
}


public class ProgressUpdateChecks : MonoBehaviour
{
    private ProgressHelper PH;
    private bool[] lastStates;
    private int checkTimer = 30; // Check every ~0.5 seconds
    private bool goalAlreadySent = false;

    private void Awake()
    {
        PH = GetComponent<ProgressHelper>();
        if (PH == null)
        {
            Plugin.Log.LogWarning("[AP] ProgressUpdateChecks added to object without ProgressHelper component!");
            Destroy(this);
            return;
        }

        if (PH.securedIslands != null)
        {
            lastStates = new bool[PH.securedIslands.Length];
            for (int i = 0; i < PH.securedIslands.Length; i++)
                lastStates[i] = PH.securedIslands[i];
        }
    }

    private void Update()
    {
        if (!Plugin.APconnected) return;
        if (checkTimer-- > 0) return;
        checkTimer = 30;

        if (PH.securedIslands == null || lastStates == null)
            return;

        bool allSecured = true;

        for (int i = 0; i < PH.securedIslands.Length; i++)
        {
            bool current = PH.securedIslands[i];
            if (current != lastStates[i])
            {
                if (current)
                {
                    Plugin.Log.LogInfo($"[AP] Island {i + 1} Liberated → Sending Check");
                    Plugin.AP?.CompleteLocation(APIDRegistry.Get($"Island {i + 1} Liberation"));
                }

                lastStates[i] = current;
            }

            if (!current)
                allSecured = false;
        }

        if (allSecured && !goalAlreadySent)
        {
            goalAlreadySent = true;
            Plugin.Log.LogInfo("[AP] All Islands Secured → Goal Achieved!");
            Plugin.AP?.Session.SetGoalAchieved();
        }
    }
}
public class StatueBlocker : MonoBehaviour
{
    private Statue statue;
    private int checkTimer = 30; // Check roughly twice per second

    private void Awake()
    {
        statue = GetComponent<Statue>();
        if (statue == null)
        {
            Plugin.Log.LogWarning("[AP] StatueBlocker added to object without Statue component!");
            Destroy(this);
            return;
        }
    }

    private void Update()
    {
        if (!Plugin.APconnected) return;
        // Only check occasionally to save performance
        if (checkTimer-- > 0) return;
        checkTimer = 30;

        bool allowed = false;
        switch (statue.deity)
        {
            case Statue.Deity.Archer:
                allowed = APItemRegistry.IsCollected(APIDRegistry.Get("Archery Use"));
                break;
            case Statue.Deity.Worker:
                allowed = APItemRegistry.IsCollected(APIDRegistry.Get("Building Use"));
                break;
            case Statue.Deity.Knight:
                allowed = APItemRegistry.IsCollected(APIDRegistry.Get("Knights Use"));
                break;
            case Statue.Deity.Farmer:
                allowed = APItemRegistry.IsCollected(APIDRegistry.Get("Scythe Use"));
                break;
        }

        statue.forceBlockPayment = !allowed;
    }
}
public class ArchipelagoPauseUI : MonoBehaviour
{
    private string host = "archipelago.gg:38281";
    private string slot = "Monarch";
    private string password = "";
    private int deathLinkMode = 0;
    private readonly string[] deathLinkOptions = new string[] { "None", "Easy", "Hard" };
    private void Awake()
    {
        // Load from your plugin config
        host = Plugin.ConfigHost.Value;
        slot = Plugin.ConfigSlot.Value;
        password = Plugin.ConfigPassword.Value;
        deathLinkMode = Plugin.ConfigDeathLinkMode.Value;
    }
    private void OnGUI()
    {
        if (!gameObject.activeSelf) return;

        // Simple static layout using GUI buttons (no TextField)
        float y = 100;
        float x = 100;

        if (!Plugin.APconnected)
        {
            GUI.Box(new Rect(x, y, 300, 210), "Archipelago Connect");
            y += 20;

            GUI.Label(new Rect(x + 10, y, 280, 25), $"Host: {host}");
            y += 20;

            GUI.Label(new Rect(x + 10, y, 280, 25), $"Slot: {slot}");
            y += 20;

            GUI.Label(new Rect(x + 10, y, 280, 25), "Death Link Mode:");
            y += 25;
            if (GUI.Button(new Rect(x + 10, y, 280, 25), deathLinkOptions[deathLinkMode]))
            {
                deathLinkMode = (deathLinkMode + 1) % deathLinkOptions.Length;
            }
            y += 30;
            if (GUI.Button(new Rect(x + 10, y, 280, 25), "Reload Config"))
            {
                Plugin.ReloadConfig();
                host = Plugin.ConfigHost.Value;
                slot = Plugin.ConfigSlot.Value;
                password = Plugin.ConfigPassword.Value;
            }
            y += 25;
            if (GUI.Button(new Rect(x + 10, y, 280, 25), "Open Config"))
                Plugin.OpenConfig();
            y += 30;

            if (GUI.Button(new Rect(x + 10, y, 280, 30), "Connect")) Connect();
        }
        else
        {
            GUI.Box(new Rect(x, y, 300, 120), $"Connected to {host} as {slot}");
            if (GUI.Button(new Rect(x + 10, y + 50, 280, 30), "Disconnect")) Disconnect();
        }
    }
    private async void Connect()
    {
        Plugin.Log.LogInfo($"[AP] Connecting to {host} as {slot} with DeathLink {deathLinkOptions[deathLinkMode]}");
        Plugin.ConfigDeathLinkMode.Value = deathLinkMode;
        Plugin.APconnected = await Plugin.AP.Connect(host, slot, password);

    }

    private async void Disconnect()
    {
        Plugin.Log.LogInfo("[AP] Disconnecting from Archipelago");
        await Plugin.AP.Session.Socket.DisconnectAsync();
        Plugin.APconnected = false;
    }
}
public class InGameLogger : MonoBehaviour
{
    private TMP_Text tmp;
    private RectTransform panel;
    private readonly Queue<string> logs = new Queue<string>();
    private const int MaxLogs = 5;

    // Target size in pixels at 1920x1080 reference resolution
    private const float RefWidth = 1920f;
    private const float RefHeight = 1080f;
    private const float RefX = 1576f;
    private const float RefY = 0f;
    private const float RefW = 344f;
    private const float RefH = 160f;

    public Canvas canvas;

    private void Awake()
    {
        canvas = PluginUI.UILogCanvas;
        if (canvas == null)
        {
            var canvasGO = new GameObject("APInGameCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // Create Panel
        var panelGO = new GameObject("APLogPanel");
        panelGO.transform.SetParent(canvas.transform);
        panel = panelGO.AddComponent<RectTransform>();

        // Anchor bottom-right
        panel.anchorMin = new Vector2(0, 0);
        panel.anchorMax = new Vector2(0, 0);
        panel.pivot = new Vector2(0, 0);

        // Scale to screen size
        float scaleX = Screen.width / RefWidth;
        float scaleY = Screen.height / RefHeight;

        panel.anchoredPosition = new Vector2(RefX * scaleX, RefY * scaleY);
        panel.sizeDelta = new Vector2(RefW * scaleX, RefH * scaleY);

        // Background (optional)
        var img = panelGO.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.5f); // semi-transparent black

        // Create TMP text
        var textGO = new GameObject("APLogText");
        textGO.transform.SetParent(panelGO.transform);
        tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.font = PluginUI.KingdomTMPFont; // Ensure font is loaded
        tmp.color = new Color32(0xff, 0xf2, 0xdb, 255);
        tmp.enableWordWrapping = true;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.fontSize = 24;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = Color.black;

        var textRT = tmp.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0, 0);
        textRT.anchorMax = new Vector2(1, 1);
        textRT.offsetMin = new Vector2(5, 5);
        textRT.offsetMax = new Vector2(-5, -5);
    }

    public void AddLog(string text)
    {

        if (tmp == null) return;

        logs.Enqueue(text);
        if (logs.Count > MaxLogs) logs.Dequeue();

        tmp.text = "";
        foreach (var log in logs)
        {
            tmp.text += $"• {log}\n";
        }
    }

    public void ClearLogs()
    {
        logs.Clear();
        if (tmp != null)
            tmp.text = "";
    }
}

public class KeyHandler : MonoBehaviour
{
    private Managers _managers;
    private int _managerSearchCooldown = 0;

    private void Update()
    {
        if (_managers == null)
        {
            TryFindManagers();
            return;
        }
        HandInput();
        if(Plugin.APconnected != PluginUI.UILogCanvas.gameObject.active)PluginUI.UILogCanvas.gameObject.SetActive(Plugin.APconnected);
    }

    private void HandInput()
    {
        if (Input.GetKeyDown(KeyCode.Keypad0))
        {

            Plugin.Log.LogInfo("Butter Fingers");

            // Get the Il2CppSystem.Type for Player
            Il2CppSystem.Type PlayerType = Il2CppSystem.Type.GetType("Player, Assembly-CSharp");

            // Find all objects of that type
            var cbObjects = UnityEngine.Object.FindObjectsOfType(PlayerType, true);
            foreach (var obj in cbObjects)
            {
                // Convert from Il2CppSystem.Object to Player safely
                var cb = Il2CppObjectPool.Get<Player>(obj.Pointer);
                while (cb.wallet.Coins >= 1)
                {
                    for (int i = 0; i < cb.wallet.Coins; i++)
                    {
                        cb.TryDropCurrency(CurrencyType.Coins);
                    }
                    for (int i = 0; i < cb.wallet.Gems; i++)
                    {
                        cb.TryDropCurrency(CurrencyType.Gems);
                    }
                }
            }
        }
        else if (Input.GetKeyDown(KeyCode.Keypad1))
        {
            Plugin.Log.LogInfo("Curse of Midas");

            // Get the Il2CppSystem.Type for Player
            Il2CppSystem.Type PlayerType = Il2CppSystem.Type.GetType("Player, Assembly-CSharp");

            // Find all objects of that type
            var cbObjects = UnityEngine.Object.FindObjectsOfType(PlayerType, true);
            foreach (var obj in cbObjects)
            {
                // Convert from Il2CppSystem.Object to Player safely
                var cb = Il2CppObjectPool.Get<Player>(obj.Pointer);
                cb.wallet.AddCurrency(CurrencyType.Coins, 99);
            }
        }
        else if (Input.GetKeyDown(KeyCode.Keypad2))
        {

            // Get the Il2CppSystem.Type for Player
            Il2CppSystem.Type PlayerType = Il2CppSystem.Type.GetType("Player, Assembly-CSharp");

            // Find all objects of that type
            var cbObjects = UnityEngine.Object.FindObjectsOfType(PlayerType, true);
            foreach (var obj in cbObjects)
            {

                var cb = Il2CppObjectPool.Get<Player>(obj.Pointer);
                if (!cb.gameObject) continue;
                if (!cb.gameObject.GetComponent<Petrifiable>()) continue;
                cb.gameObject.GetComponent<Petrifiable>().StartPetrify(1, 30);
            }
        }
        else if (Input.GetKeyDown(KeyCode.Keypad3))
        {
            Plugin.Log.LogInfo("Lost Crown Debug");

            // Get the Il2CppSystem.Type for Player
            Il2CppSystem.Type PlayerType = Il2CppSystem.Type.GetType("Player, Assembly-CSharp");

            // Find all objects of that type
            var cbObjects = UnityEngine.Object.FindObjectsOfType(PlayerType, true);
            foreach (var obj in cbObjects)
            {
                // Convert from Il2CppSystem.Object to Player safely
                var cb = Il2CppObjectPool.Get<Player>(obj.Pointer);
                if (cb.hasCrown) cb.LoseCrownDebug();
            }
        }
        else if (Input.GetKeyDown(KeyCode.Keypad4)) DeathLink();
        else if (Input.GetKeyDown(KeyCode.Keypad5)) PluginUI.Logger.AddLog("Received Sword of Destiny!");
        else if (Input.GetKeyDown(KeyCode.Keypad6)) PluginUI.Logger.ClearLogs();
    }
    public void DeathLink()
    {
        Plugin.Log.LogInfo("Deaht Link test");

        // Get the Il2CppSystem.Type for Player
        Il2CppSystem.Type PlayerType = Il2CppSystem.Type.GetType("Player, Assembly-CSharp");

        // Find all objects of that type
        var cbObjects = UnityEngine.Object.FindObjectsOfType(PlayerType, true);
        foreach (var obj in cbObjects)
        {
            // Convert from Il2CppSystem.Object to Player safely
            var cb = Il2CppObjectPool.Get<Player>(obj.Pointer);
            cb.OnReceiveDamageHandler(1, gameObject, DamageSource.Fire);
        }
    }
    private void TryFindManagers()
    {
        if (_managerSearchCooldown > 0)
        {
            _managerSearchCooldown--;
            return;
        }

        _managers = FindObjectOfType<Managers>(true);
        if (_managers != null)
        {
            Plugin.Log.LogInfo("Managers found.");
        }
        else
        {
            _managerSearchCooldown = 100; // Retry every 100 frames
        }
    }

}


public static class JsonHelper
{
    private static readonly MethodInfo SerializeMethod;

    static JsonHelper()
    {
        // Get the SerializeObject(object) method from the game’s Newtonsoft
        SerializeMethod = typeof(JsonConvert).GetMethod(
            "SerializeObject", 
            new Type[] { typeof(object) }
        );

        if (SerializeMethod == null)
        {
            throw new Exception("JsonConvert.SerializeObject(object) not found in the game DLL!");
        }
    }

    public static string Serialize(object obj)
    {
        return (string)SerializeMethod.Invoke(null, new object[] { obj });
    }
}