using BepInEx;
using Aki.Reflection.Patching;
using System.Reflection;
using UnityEngine;
using EFT.Communications;
using EFT;
using System.Collections.Generic;
using BepInEx.Configuration;
using Comfort.Common;

namespace BossNotifier {
    [BepInPlugin("Mattdokn.BossNotifier", "BossNotifier", "1.2.0")]
    public class BossNotifierPlugin : BaseUnityPlugin {
        public static ConfigEntry<KeyboardShortcut> showBossesKeyCode;
        public static ConfigEntry<bool> showNotificationsOnRaidStart;
        public static ConfigEntry<int> intelCenterUnlockLevel;
        public static ConfigEntry<bool> showBossLocation;
        public static ConfigEntry<int> intelCenterLocationUnlockLevel;

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
        // Empty string for no location/everywhere on factory, null for unknown zone
        public static readonly Dictionary<string, string> zoneNames = new Dictionary<string, string>() {
            {"ZoneScavBase", "Scav Base" },
            {"ZoneDormitory", "Dormitory" },
            {"ZoneGasStation", "Gas Station" },
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
            showBossesKeyCode = Config.Bind("Boss Notifier", "Keyboard Shortcut", new KeyboardShortcut(KeyCode.O), "Key to show boss notifications.");
            showNotificationsOnRaidStart = Config.Bind("Boss Notifier", "Show Bosses on Raid Start", true, "Show bosses on raid start.");
            intelCenterUnlockLevel = Config.Bind<int>("Balance", "Intel Center Level Requirement", 0, "Level to unlock at.");


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
            }
        }

        public static string GetBossName(WildSpawnType type) {
            // If type is in bossNames, return value, otherwise return null
            return bossNames.ContainsKey(type) ? bossNames[type] : null;
        }

        public static string GetZoneName(string zoneId) {
            return zoneNames.ContainsKey(zoneId) ? zoneNames[zoneId] : null;
        }
    }

    internal class BossLocationSpawnPatch : ModulePatch {
        protected override MethodBase GetTargetMethod() => typeof(BossLocationSpawn).GetMethod("Init");

        public static HashSet<string> bossesInRaid = new HashSet<string>();

        [PatchPostfix]
        private static void PatchPostfix(BossLocationSpawn __instance) {
            if (__instance.ShallSpawn) {
                string name = BossNotifierPlugin.GetBossName(__instance.BossType);
                if (name == null) return;

                bossesInRaid.Add(__instance.BossZone);
                bossesInRaid.Add(name);
            }
        }
    }

    internal class NewGamePatch : ModulePatch {
        protected override MethodBase GetTargetMethod() => typeof(GameWorld).GetMethod("OnGameStarted");

        [PatchPrefix]
        public static void PatchPrefix() {
            int intelCenterLevel = Singleton<GameWorld>.Instance.MainPlayer.Profile.Hideout.Areas[11].level;
            if (intelCenterLevel >= BossNotifierPlugin.intelCenterUnlockLevel.Value) {
                BossNotifierMono.Init();
            }
        }
    }

    class BossNotifierMono : MonoBehaviour {
        private void SendBossNotifications() {
            foreach (var boss in BossLocationSpawnPatch.bossesInRaid) {
                NotificationManagerClass.DisplayMessageNotification($"{boss} will spawn.", ENotificationDurationType.Long);
            }
        }

        public static void Init() {
            if (Singleton<IBotGame>.Instantiated) {
                GClass5.GetOrAddComponent<BossNotifierMono>(Singleton<GameWorld>.Instance);
            }
        }

        public void Awake() {
            if (!BossNotifierPlugin.showNotificationsOnRaidStart.Value) return;
            SendBossNotifications();
        }

        public void Update() {
            if (BossNotifierPlugin.showBossesKeyCode.Value.IsDown()) {
                SendBossNotifications();
            }
        }

        public void OnDestroy() {
            BossLocationSpawnPatch.bossesInRaid.Clear();
        }
    }
}
