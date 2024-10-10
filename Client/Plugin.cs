using BepInEx;
using SPT.Reflection.Patching;
using SPT.Reflection.Utils;
using System.Reflection;
using UnityEngine;
using EFT.Communications;
using EFT;
using System.Collections.Generic;
using BepInEx.Configuration;
using Comfort.Common;
using BepInEx.Logging;
using System.Text;
using BepInEx.Bootstrap;
using System;
using System.Linq;
using HarmonyLib;
using SPT.Common.Http;
using Newtonsoft.Json;

namespace BossNotifier {
    [BepInPlugin("Mattdokn.BossNotifier", "BossNotifier", "1.5.1.1")]
    [BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
    public class BossNotifierPlugin : BaseUnityPlugin {
        public static FieldInfo FikaIsPlayerHost;


        // Configuration entries
        public static ConfigEntry<KeyboardShortcut> showBossesKeyCode;
        public static ConfigEntry<bool> showNotificationsOnRaidStart;
        public static ConfigEntry<int> intelCenterUnlockLevel;
        // public static ConfigEntry<bool> showBossLocation;
        public static ConfigEntry<int> intelCenterLocationUnlockLevel;
        // public static ConfigEntry<bool> showBossDetected;
        public static ConfigEntry<int> intelCenterDetectedUnlockLevel;

        private static ManualLogSource logger;


        // Logging methods
        public static void Log(LogLevel level, string msg) {
            logger.Log(level, msg);
        }

        // Dictionary mapping boss types to names
        public static readonly Dictionary<WildSpawnType, string> bossNames = new Dictionary<WildSpawnType, string>() {
            { WildSpawnType.bossBully, "Reshala" },
            { WildSpawnType.bossKnight, "Goons" },
            { WildSpawnType.sectantPriest, "Cultists" },
            { WildSpawnType.bossTagilla, "Tagilla" },
            { WildSpawnType.bossKilla, "Killa" },
            { WildSpawnType.bossZryachiy, "Zryachiy" },
            { WildSpawnType.bossGluhar, "Glukhar" },
            { WildSpawnType.bossSanitar, "Sanitar" },
            { WildSpawnType.bossKojaniy, "Shturman" },
            { WildSpawnType.bossBoar, "Kaban" },
            { WildSpawnType.gifter, "Santa Claus" },
            { WildSpawnType.arenaFighterEvent, "Blood Hounds" },
            { WildSpawnType.crazyAssaultEvent, "Crazy Scavs" },
            { WildSpawnType.exUsec, "Rogues" },
            { WildSpawnType.bossKolontay, "Kollontay" },
            { (WildSpawnType)4206927, "Punisher" },
            { (WildSpawnType)199, "Legion" },
        };
        // Set of plural boss names
        public static readonly HashSet<string> pluralBosses = new HashSet<string>() {
            "Goons",
            "Cultists",
            "Blood Hounds",
            "Crazy Scavs",
            "Rogues",
        };
        // Dictionary mapping zone IDs to names
        public static readonly Dictionary<string, string> zoneNames = new Dictionary<string, string>() {
            {"ZoneScavBase", "Scav Base" },
            {"ZoneDormitory", "Dormitory" },
            {"ZoneGasStation", "Gas Station" },
            {"ZoneTankSquare", "Old Construction" },
            {"ZoneWade", "RUAF Roadblock" },
            {"BotZone", "" },
            {"ZoneCenterBot", "Center Floor 2" },
            {"ZoneCenter", "Center Floor 1" },
            {"ZoneOLI", "OLI" },
            {"ZoneIDEA", "IDEA" },
            {"ZoneGoshan", "Goshan" },
            {"ZoneIDEAPark", "IDEA Parking" },
            {"ZoneOLIPark", "OLI Parking" },
            {"BotZoneFloor1", "Floor 1" },
            {"BotZoneFloor2", "Floor 2" },
            {"BotZoneBasement", "Basement" },
            {"BotZoneGate1", "Gate 1" },
            {"BotZoneGate2", "Gate 2" },
            {"ZoneRailStrorage", "Rail Storage" },
            {"ZonePTOR1", "Black Pawn" },
            {"ZonePTOR2", "White Knight" },
            {"ZoneBarrack", "Barracks" },
            {"ZoneSubStorage", "Sub Storage Д" },
            {"ZoneSubCommand", "Sub Command Д" },
            {"ZoneForestGasStation", "Forest Gas Station" },
            {"ZoneForestSpawn", "Forest" },
            {"ZonePort", "Pier" },
            {"ZoneSanatorium1", "Sanatorium West" },
            {"ZoneSanatorium2", "Sanatorium East" },
            {"ZoneMiniHouse", "Mini House" },
            {"ZoneBrokenVill", "Broken Village" },
            {"ZoneWoodCutter", "Wood Cutter" },
            {"ZoneCard1", "Card 1" },
        };

        private void Awake() {
            logger = Logger;

            Type FikaUtilExternalType = Type.GetType("Fika.Core.Coop.Utils.FikaBackendUtils, Fika.Core", false);
            if (FikaUtilExternalType != null) {
                FikaIsPlayerHost = AccessTools.Field(FikaUtilExternalType, "MatchingType");
            }

            // Initialize configuration entries
            showBossesKeyCode = Config.Bind("General", "Keyboard Shortcut", new KeyboardShortcut(KeyCode.O), "Key to show boss notifications.");
            showNotificationsOnRaidStart = Config.Bind("General", "Show Bosses on Raid Start", true, "Show boss notifications on raid start.");
            // showBossLocation = Config.Bind("Balance", "Show Boss Spawn Location", true, "Show boss locations in notification.");
            // showBossDetected = Config.Bind("In-Raid Updates", "Show Boss Detected Notification", true, "Show detected notification when bosses spawn during the raid.");
            // intelCenterUnlockLevel = Config.Bind("Balance", "Intel Center Level Requirement", 0, "Level to unlock at.");
            intelCenterUnlockLevel = Config.Bind("Intel Center Unlocks (4 means Disabled)", "1. Intel Center Level Requirement", 0, 
                new ConfigDescription("Level to unlock plain notifications at.",
                new AcceptableValueRange<int>(0, 4)));
            // intelCenterLocationUnlockLevel = Config.Bind("Balance", "Intel Center Location Level Requirement", 0, "Unlocks showing boss spawn location.");
            intelCenterLocationUnlockLevel = Config.Bind("Intel Center Unlocks (4 means Disabled)", "2. Intel Center Location Level Requirement", 0,
                new ConfigDescription("Unlocks showing boss spawn location in notification.",
                new AcceptableValueRange<int>(0, 4)));
            // intelCenterDetectedUnlockLevel = Config.Bind("Intel Center Unlocks", "Intel Center Detection Requirement", 0, "Unlocks showing boss detected notification.");
            intelCenterDetectedUnlockLevel = Config.Bind("Intel Center Unlocks (4 means Disabled)", "3. Intel Center Detection Requirement", 0, 
                new ConfigDescription("Unlocks showing boss detected notification. (When you get near a boss)", 
                new AcceptableValueRange<int>(0, 4)));


            // Enable patches
            new BossLocationSpawnPatch().Enable();
            new NewGamePatch().Enable();
            new BotBossPatch().Enable();

            // Subscribe to config changes
            Config.SettingChanged += Config_SettingChanged;

            Logger.LogInfo($"Plugin BossNotifier is loaded!");
        }

        // Event handler for configuration changes
        private void Config_SettingChanged(object sender, SettingChangedEventArgs e) {
            ConfigEntryBase changedSetting = e.ChangedSetting;

            // If player is in a raid, reset their notifications to reflect changes
            if (BossNotifierMono.Instance) BossNotifierMono.Instance.GenerateBossNotifications();
        }

        // Get boss name by type
        public static string GetBossName(WildSpawnType type) {
            // Return boss name if found, otherwise null
            return bossNames.ContainsKey(type) ? bossNames[type] : null;
        }

        // Get zone name by ID
        public static string GetZoneName(string zoneId) {
            // Return zone name if found, otherwise clean up the zoneId
            if (zoneNames.ContainsKey(zoneId)) return zoneNames[zoneId];

            string location = zoneId.Replace("Bot", "").Replace("Zone", "");
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < location.Length; i++) {
                char c = location[i];
                if (char.IsUpper(c) && i != 0 && i < location.Length - 1 && !char.IsUpper(location[i + 1]) && !char.IsDigit(location[i + 1])) {
                    sb.Append(" ");
                }
                sb.Append(c);
            }
            return sb.ToString().Replace("_", " ").Trim();
        }
    }

    // Patch for tracking boss location spawns
    internal class BossLocationSpawnPatch : ModulePatch {
        protected override MethodBase GetTargetMethod() => typeof(BossLocationSpawn).GetMethod("Init");

        // Bosses in raid along with their locations ex Key: Reshala Value: Dorms, Gas Station
        public static Dictionary<string, string> bossesInRaid = new Dictionary<string, string>();

        // Add boss spawn if not already present
        private static void TryAddBoss(string boss, string location) {
            if (location == null) {
                Logger.LogError("Tried to add boss with null location.");
                return;
            }
            // If boss is already added
            if (bossesInRaid.ContainsKey(boss)) {
                // If location isn't already present, and location isnt empty, add it.
                if (!bossesInRaid[boss].Contains(location) && !location.Equals("")) {
                    // If the boss has an empty location, set new location
                    if (bossesInRaid[boss].Equals("")) {
                        bossesInRaid[boss] = location;
                    } else {
                        // Otherwise if boss has a location, append our new location
                        bossesInRaid[boss] += ", " + location;
                    }
                }
            } else {
                // Add the boss entry
                bossesInRaid.Add(boss, location);
            }
        }

        // Handle boss location spawns
        [PatchPostfix]
        private static void PatchPostfix(BossLocationSpawn __instance) {
            // If the boss will spawn
            if (__instance.ShallSpawn) {
                // Get it's name, if no name found then return.
                string name = BossNotifierPlugin.GetBossName(__instance.BossType);
                if (name == null) return;

                // Get the spawn location
                string location = BossNotifierPlugin.GetZoneName(__instance.BornZone);

                BossNotifierPlugin.Log(LogLevel.Debug, $"Boss {name} @ zone {__instance.BornZone} translated to {(location == null ? __instance.BornZone.Replace("Bot", "").Replace("Zone", ""): location)}");

                if (location == null) {
                    // If it's null then use cleaned up BornZone
                    TryAddBoss(name, __instance.BornZone.Replace("Bot", "").Replace("Zone", ""));
                } else if (location.Equals("")) {
                    // If it's empty location (Factory Spawn)
                    TryAddBoss(name, "");
                } else {
                    // Location is valid
                    TryAddBoss(name, location);
                }
            }
        }
    }

    // Patch for tracking live boss spawns
    internal class BotBossPatch : ModulePatch {
        protected override MethodBase GetTargetMethod() => typeof(BotBoss).GetConstructors()[0];

        // Bosses spawned in raid
        public static HashSet<string> spawnedBosses = new HashSet<string>();

        [PatchPostfix]
        private static void PatchPostfix(BotBoss __instance) {
            WildSpawnType role = __instance.Owner.Profile.Info.Settings.Role;
            // Get it's name, if no name found then return.
            string name = BossNotifierPlugin.GetBossName(role);
            if (name == null) return;

            // Get the spawn location
            Vector3 positionVector = __instance.Player().Position;
            string position = $"{(int)positionVector.x}, {(int)positionVector.y}, {(int)positionVector.z}";
            // {name} has spawned at (x, y, z) on {map}
            BossNotifierPlugin.Log(LogLevel.Info, $"{name} has spawned at {position} on {Singleton<GameWorld>.Instance.LocationId}");

            // Add boss to spawnedBosses
            spawnedBosses.Add(name);

            if (BossNotifierMono.Instance.intelCenterLevel >= BossNotifierPlugin.intelCenterDetectedUnlockLevel.Value) {
                NotificationManagerClass.DisplayMessageNotification($"{name} {(BossNotifierPlugin.pluralBosses.Contains(name) ? "have" : "has")} been detected in your vicinity.", ENotificationDurationType.Long);
                BossNotifierMono.Instance.GenerateBossNotifications();
            }
        }
    }

    // Patch for hooking when a raid is started
    internal class NewGamePatch : ModulePatch {
        protected override MethodBase GetTargetMethod() => typeof(GameWorld).GetMethod("OnGameStarted");

        [PatchPrefix]
        public static void PatchPrefix() {
            // Start BossNotifierMono
            BossNotifierMono.Init();
        }
    }

    // Monobehavior for boss notifier
    class BossNotifierMono : MonoBehaviour {
        class BossList {
            public Dictionary<string, string> bosses;
        }
        // Required to invalidate notification cache on settings changed event.
        public static BossNotifierMono Instance;
        // Caching the notification messages
        private List<string> bossNotificationMessages;
        // Intel Center level, only updated when raid is entered.
        public int intelCenterLevel;

        private void SendBossNotifications() {
            // this should be removed as we're gonna get the data from server now if its a client
            if (intelCenterLevel < BossNotifierPlugin.intelCenterUnlockLevel.Value) return;

            // If we have no notifications to display, send one saying there's no bosses located.
            if (bossNotificationMessages.Count == 0) {
                NotificationManagerClass.DisplayMessageNotification("No Bosses Located", ENotificationDurationType.Long);
                return;
            }

            foreach (var bossMessage in bossNotificationMessages) {
                NotificationManagerClass.DisplayMessageNotification(bossMessage, ENotificationDurationType.Long);
            }
        }

        // Initializes boss notifier mono and attaches it to the game world object
        public static void Init() {
            BotBossPatch.spawnedBosses.Clear();
            if (Singleton<GameWorld>.Instantiated) {
                Instance = Singleton<GameWorld>.Instance.GetOrAddComponent<BossNotifierMono>();
                BossNotifierPlugin.Log(LogLevel.Info, $"Game started on map {Singleton<GameWorld>.Instance.LocationId}");
                if (ClientAppUtils.GetMainApp().GetClientBackEndSession() == null) {
                    Instance.intelCenterLevel = 0;
                } else {
                    Instance.intelCenterLevel = ClientAppUtils.GetMainApp().GetClientBackEndSession().Profile.Hideout.Areas[11].level;
                }
            }
        }

        public void Start() {
            if (IsHost()) {
                // Send out the boss list to the server (if we're using fika and the host!)
                RequestHandler.PostJsonAsync("/setbosses/", JsonConvert.SerializeObject(BossLocationSpawnPatch.bossesInRaid));
                // Generate notifications
                GenerateBossNotifications();

                if (!BossNotifierPlugin.showNotificationsOnRaidStart.Value) return;
                Invoke("SendBossNotifications", 2f);
            } else {
                // We're the client, wait 2 seconds THEN generate boss notifications and invoke sendbossnotifications
                Invoke("FikaClientSpecificInit", 2f);
            }


        }

        public void FikaClientSpecificInit() {
            // Generate notifications
            GenerateBossNotifications();

            if (!BossNotifierPlugin.showNotificationsOnRaidStart.Value) return;
            SendBossNotifications();
        }

        public void Update() {
            if (IsKeyPressed(BossNotifierPlugin.showBossesKeyCode.Value)) {
                SendBossNotifications();
            }
        }

        public void OnDestroy() {
            // Clear out boss locations for this raid
            BossLocationSpawnPatch.bossesInRaid.Clear();
            // Clear out spawned bosses for this raid
            BotBossPatch.spawnedBosses.Clear();
        }

        public bool IsHost() {
            if (BossNotifierPlugin.FikaIsPlayerHost == null) {
                return true;
            }
            return (int)BossNotifierPlugin.FikaIsPlayerHost.GetValue(null) == 2;
        }

        public void UpdateBossListFromServer() {
            string req = RequestHandler.GetJson("/getbosses/");
            var bosses = JsonConvert.DeserializeObject<BossList>(req);

            foreach (var boss in bosses.bosses) {
                BossLocationSpawnPatch.bossesInRaid.Add(boss.Key, boss.Value);
            }
        }

        public void GenerateBossNotifications() {
            if (!IsHost()) {
                // we're a client, grab the bosslist from the server
                UpdateBossListFromServer();
            }

            // Clear out boss notification cache
            bossNotificationMessages = new List<string>();

            // Check if it's daytime to prevent showing Cultist notif.
            // This is the same method that DayTimeCultists patches so if that mod is installed then this always returns false
            bool isDayTime = Singleton<IBotGame>.Instance.BotsController.ZonesLeaveController.IsDay();

            // Get whether location is unlocked or not.
            bool isLocationUnlocked = intelCenterLevel >= BossNotifierPlugin.intelCenterLocationUnlockLevel.Value;

            // Get whether detection is unlocked or not.
            bool isDetectionUnlocked = intelCenterLevel >= BossNotifierPlugin.intelCenterDetectedUnlockLevel.Value;

            foreach (var bossSpawn in BossLocationSpawnPatch.bossesInRaid) {
                // If it's daytime then cultists don't spawn
                if (isDayTime && bossSpawn.Key.Equals("Cultists")) continue;

                // If boss has been spawned/detected
                bool isDetected = BotBossPatch.spawnedBosses.Contains(bossSpawn.Key);

                string notificationMessage;
                // If we don't have locations or value is null/whitespace
                if (!isLocationUnlocked || bossSpawn.Value == null || bossSpawn.Value.Equals("")) {
                    // Then just show that they spawned and nothing else
                    notificationMessage = $"{bossSpawn.Key} {(BossNotifierPlugin.pluralBosses.Contains(bossSpawn.Key) ? "have" : "has")} been located.{(isDetectionUnlocked && isDetected ? $" ✓" : "")}";
                } else {
                    // Location is unlocked and location isnt null
                    notificationMessage = $"{bossSpawn.Key} {(BossNotifierPlugin.pluralBosses.Contains(bossSpawn.Key) ? "have" : "has")} been located near {bossSpawn.Value}{(isDetectionUnlocked && isDetected ? $" ✓" : "")}";
                }
                // Add notification to cache list
                bossNotificationMessages.Add(notificationMessage);
            }

        }

        // Credit to DrakiaXYZ, thank you!
        bool IsKeyPressed(KeyboardShortcut key) {
            if (!UnityInput.Current.GetKeyDown(key.MainKey)) {
                return false;
            }
            foreach (var modifier in key.Modifiers) {
                if (!UnityInput.Current.GetKey(modifier)) {
                    return false;
                }
            }
            return true;
        }
    }
}
