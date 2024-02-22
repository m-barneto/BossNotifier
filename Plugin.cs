using BepInEx;
using Aki.Reflection.Patching;
using System.Reflection;
using UnityEngine;
using EFT.Communications;
using EFT;
using System.Collections.Generic;
using BepInEx.Configuration;
using Comfort.Common;
using BepInEx.Logging;
using System.Linq;
using BepInEx.Bootstrap;

namespace BossNotifier {
    [BepInPlugin("Mattdokn.BossNotifier", "BossNotifier", "1.3.2")]
    public class BossNotifierPlugin : BaseUnityPlugin {
        // Configuration entries
        public static ConfigEntry<KeyboardShortcut> showBossesKeyCode;
        public static ConfigEntry<bool> showNotificationsOnRaidStart;
        public static ConfigEntry<int> intelCenterUnlockLevel;
        public static ConfigEntry<bool> showBossLocation;
        public static ConfigEntry<int> intelCenterLocationUnlockLevel;

        private static ManualLogSource logger;

        // Logging method
        public static void LogInfo(string msg) {
            logger.LogInfo(msg);
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
            {"BotZone", "" },
            {"ZoneCenterBot", "Center" },
            {"ZoneCenter", "Center" },
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
            {"ZonePTOR1", "White Knight" },
            {"ZonePTOR2", "Black Pawn" },
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
        };

        private void Awake() {
            logger = Logger;

            // Initialize configuration entries
            showBossesKeyCode = Config.Bind("Boss Notifier", "Keyboard Shortcut", new KeyboardShortcut(KeyCode.O), "Key to show boss notifications.");
            showNotificationsOnRaidStart = Config.Bind("Boss Notifier", "Show Bosses on Raid Start", true, "Show bosses on raid start.");
            intelCenterUnlockLevel = Config.Bind("Balance", "Intel Center Level Requirement", 0, "Level to unlock at.");
            showBossLocation = Config.Bind("Balance", "Show Boss Spawn Location", true, "Show boss locations in notification.");
            intelCenterLocationUnlockLevel = Config.Bind("Balance", "Intel Center Location Level Requirement", 0, "Unlocks showing boss spawn location.");

            // Enable patches
            new BossLocationSpawnPatch().Enable();
            new NewGamePatch().Enable();

            // Subscribe to config changes
            Config.SettingChanged += Config_SettingChanged;

            Logger.LogInfo($"Plugin BossNotifier is loaded!");
        }

        // Event handler for configuration changes
        private void Config_SettingChanged(object sender, SettingChangedEventArgs e) {
            ConfigEntryBase changedSetting = e.ChangedSetting;

            // Clamp Intel Levels to valid values.
            if (changedSetting.Definition.Key.Equals("Intel Center Level Requirement")) {
                if (intelCenterUnlockLevel.Value < 0) intelCenterUnlockLevel.Value = 0;
                else if (intelCenterUnlockLevel.Value > 3) intelCenterUnlockLevel.Value = 3;
            } else if (changedSetting.Definition.Key.Equals("Intel Center Location Level Requirement")) {
                if (intelCenterLocationUnlockLevel.Value < 0) intelCenterLocationUnlockLevel.Value = 0;
                else if (intelCenterLocationUnlockLevel.Value > 3) intelCenterLocationUnlockLevel.Value = 3;
            }

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
            // Return zone name if found, otherwise null
            return zoneNames.ContainsKey(zoneId) ? zoneNames[zoneId] : null;
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

    // Patch for hooking when a raid is started
    internal class NewGamePatch : ModulePatch {
        protected override MethodBase GetTargetMethod() => typeof(GameWorld).GetMethod("OnGameStarted");

        [PatchPrefix]
        public static void PatchPrefix() {
            // If intel center level allows us to access notifications then start BossNotifierMono
            int intelCenterLevel = Singleton<GameWorld>.Instance.MainPlayer.Profile.Hideout.Areas[11].level;
            if (intelCenterLevel >= BossNotifierPlugin.intelCenterUnlockLevel.Value) {
                BossNotifierMono.Init(intelCenterLevel);
            }
        }
    }

    // Monobehavior for boss notifier
    class BossNotifierMono : MonoBehaviour {
        // Required to invalidate notification cache on settings changed event.
        public static BossNotifierMono Instance;
        // Caching the notification messages
        private List<string> bossNotificationMessages;
        // Intel Center level, only updated when raid is entered.
        private int intelCenterLevel;

        private void SendBossNotifications() {
            if (intelCenterLevel < BossNotifierPlugin.intelCenterUnlockLevel.Value) return;

            foreach (var bossMessage in bossNotificationMessages) {
                NotificationManagerClass.DisplayMessageNotification(bossMessage, ENotificationDurationType.Long);
            }
        }

        // Initializes boss notifier mono and attaches it to the game world object
        public static void Init(int intelCenterLevel) {
            if (Singleton<IBotGame>.Instantiated) {
                Instance = GClass5.GetOrAddComponent<BossNotifierMono>(Singleton<GameWorld>.Instance);
                Instance.intelCenterLevel = intelCenterLevel;
            }
        }

        public void Start() {
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
        }

        public void GenerateBossNotifications() {
            // Clear out boss notification cache
            bossNotificationMessages = new List<string>();

            // Check if it's daytime to prevent showing Cultist notif.
            // This is the same method that DayTimeCultists patches so if that mod is installed then this always returns false
            bool isDayTime = Singleton<IBotGame>.Instance.BotsController.ZonesLeaveController.IsDay();

            // Get whether location is unlocked or not.
            int intelCenterLevel = Singleton<GameWorld>.Instance.MainPlayer.Profile.Hideout.Areas[11].level;
            bool isLocationUnlocked = BossNotifierPlugin.showBossLocation.Value && (intelCenterLevel >= BossNotifierPlugin.intelCenterLocationUnlockLevel.Value);

            foreach (var bossSpawn in BossLocationSpawnPatch.bossesInRaid) {
                // If it's daytime then cultists don't spawn
                if (isDayTime && bossSpawn.Key.Equals("Cultists")) continue;

                string notificationMessage;
                // If we don't have locations or value is null/whitespace
                if (!isLocationUnlocked || bossSpawn.Value == null || bossSpawn.Value.Equals("")) {
                    // Then just show that they spawned and nothing else
                    notificationMessage = $"{bossSpawn.Key} {(BossNotifierPlugin.pluralBosses.Contains(bossSpawn.Key) ? "have" : "has")} spawned.";
                } else {
                    // Location is unlocked and location isnt null
                    notificationMessage = $"{bossSpawn.Key} @ {bossSpawn.Value}";
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