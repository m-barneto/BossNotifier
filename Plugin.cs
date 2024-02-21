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

namespace BossNotifier {
    [BepInPlugin("Mattdokn.BossNotifier", "BossNotifier", "1.3.1")]
    public class BossNotifierPlugin : BaseUnityPlugin {
        public static ConfigEntry<KeyboardShortcut> showBossesKeyCode;
        public static ConfigEntry<bool> showNotificationsOnRaidStart;
        public static ConfigEntry<int> intelCenterUnlockLevel;
        public static ConfigEntry<bool> showBossLocation;
        public static ConfigEntry<int> intelCenterLocationUnlockLevel;

        private static ManualLogSource logger;

        public static void LogInfo(string msg) {
            logger.LogInfo(msg);
        }

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
        public static readonly HashSet<string> pluralBosses = new HashSet<string>() {
            "Goons",
            "Cultists",
            "Blood Hounds",
            "Crazy Scavs",
            "Rogues",
        };
        // Empty string for no location/everywhere on factory, null for unknown zone
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

            showBossesKeyCode = Config.Bind("Boss Notifier", "Keyboard Shortcut", new KeyboardShortcut(KeyCode.O), "Key to show boss notifications.");
            showNotificationsOnRaidStart = Config.Bind("Boss Notifier", "Show Bosses on Raid Start", true, "Show bosses on raid start.");
            intelCenterUnlockLevel = Config.Bind("Balance", "Intel Center Level Requirement", 0, "Level to unlock at.");
            showBossLocation = Config.Bind("Balance", "Show Boss Spawn Location", true, "Show boss locations in notification.");
            intelCenterLocationUnlockLevel = Config.Bind("Balance", "Intel Center Location Level Requirement", 0, "Unlocks showing boss spawn location.");


            new BossLocationSpawnPatch().Enable();
            new NewGamePatch().Enable();

            base.Config.SettingChanged += this.Config_SettingChanged;

            Logger.LogInfo($"Plugin BossNotifier is loaded!");
        }

        private void Config_SettingChanged(object sender, SettingChangedEventArgs e) {
            ConfigEntryBase changedSetting = e.ChangedSetting;
            if (changedSetting.Definition.Key.Equals("Intel Center Level Requirement")) {
                if (intelCenterUnlockLevel.Value < 0) intelCenterUnlockLevel.Value = 0;
                else if (intelCenterUnlockLevel.Value > 3) intelCenterUnlockLevel.Value = 3;
            } else if (changedSetting.Definition.Key.Equals("Intel Center Location Level Requirement")) {
                if (intelCenterLocationUnlockLevel.Value < 0) intelCenterLocationUnlockLevel.Value = 0;
                else if (intelCenterLocationUnlockLevel.Value > 3) intelCenterLocationUnlockLevel.Value = 3;
            }
        }

        public static string GetBossName(WildSpawnType type) {
            // If type is in bossNames, return value, otherwise return null
            return bossNames.ContainsKey(type) ? bossNames[type] : null;
        }

        public static string GetZoneName(string zoneId) {
            // If zoneId is in zoneNames, return value, otherwise return null
            return zoneNames.ContainsKey(zoneId) ? zoneNames[zoneId] : null;
        }
    }

    internal class BossLocationSpawnPatch : ModulePatch {
        protected override MethodBase GetTargetMethod() => typeof(BossLocationSpawn).GetMethod("Init");

        public static Dictionary<string, string> bossesInRaid = new Dictionary<string, string>();

        private static void TryAddBoss(string boss, string location) {
            if (location == null) {
                Logger.LogError("Tried to add boss with null location!");
                return;
            }
            // If boss is already added
            if (bossesInRaid.ContainsKey(boss)) {
                // If location is present, append the new location
                // If new location isnt logged, isn't empty, and previous
                if (!bossesInRaid[boss].Contains(location) && !location.Equals("")) {
                    if (bossesInRaid[boss].Equals("")) {
                        bossesInRaid[boss] = location;
                    } else {
                        bossesInRaid[boss] += ", " + location;
                    }
                }
            } else {
                // Add the boss entry
                bossesInRaid.Add(boss, location);
            }
        }

        [PatchPostfix]
        private static void PatchPostfix(BossLocationSpawn __instance) {
            if (__instance.ShallSpawn) {
                string name = BossNotifierPlugin.GetBossName(__instance.BossType);
                if (name == null) return;

                string location = BossNotifierPlugin.GetZoneName(__instance.BornZone);

                if (!BossNotifierPlugin.showBossLocation.Value || location == null || location.Equals("")) {
                    if (location == null) {
                        // Unknown location
                        TryAddBoss(name, __instance.BornZone);
                    } else {
                        TryAddBoss(name, "");
                    }
                } else {
                    // Location is unlocked and location isnt null
                    TryAddBoss(name, location);
                }
            }
        }
    }

    internal class NewGamePatch : ModulePatch {
        protected override MethodBase GetTargetMethod() => typeof(GameWorld).GetMethod("OnGameStarted");

        [PatchPrefix]
        public static void PatchPrefix() {
            int intelCenterLevel = Singleton<GameWorld>.Instance.MainPlayer.Profile.Hideout.Areas[11].level;
            if (intelCenterLevel >= BossNotifierPlugin.intelCenterUnlockLevel.Value) {
                BossNotifierMono.Init(intelCenterLevel);
            }
        }
    }

    class BossNotifierMono : MonoBehaviour {
        private static bool isLocationUnlocked;
        private List<string> bossNotificationMessages = new List<string>();

        private void SendBossNotifications() {
            foreach (var bossMessage in bossNotificationMessages) {
                NotificationManagerClass.DisplayMessageNotification(bossMessage, ENotificationDurationType.Long);
            }
        }

        public static void Init(int intelCenterLevel) {
            if (Singleton<IBotGame>.Instantiated) {
                isLocationUnlocked = intelCenterLevel >= BossNotifierPlugin.intelCenterLocationUnlockLevel.Value;

                GClass5.GetOrAddComponent<BossNotifierMono>(Singleton<GameWorld>.Instance);
            }
        }

        public void Start() {
            bool isNight = Time.time - 100f > 1f;
            foreach (var bossSpawn in BossLocationSpawnPatch.bossesInRaid) {
                if (isNight && bossSpawn.Key.Equals("Cultists")) continue;

                string notificationMessage;
                if (!isLocationUnlocked || bossSpawn.Value == null || bossSpawn.Value.Equals("")) {
                    notificationMessage = $"{bossSpawn.Key} {(BossNotifierPlugin.pluralBosses.Contains(bossSpawn.Key) ? "have" : "has")} spawned.";
                } else {
                    // Location is unlocked and location isnt null
                    notificationMessage = $"{bossSpawn.Key} @ {bossSpawn.Value}";
                }
                bossNotificationMessages.Add(notificationMessage);
            }

            if (!BossNotifierPlugin.showNotificationsOnRaidStart.Value) return;
            SendBossNotifications();
        }

        public void Update() {
            if (IsKeyPressed(BossNotifierPlugin.showBossesKeyCode.Value)) {
                SendBossNotifications();
            }
        }

        public void OnDestroy() {
            BossLocationSpawnPatch.bossesInRaid.Clear();
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