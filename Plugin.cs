using BepInEx;
using SPT.Reflection.Patching;
using SPT.Reflection.Utils;
using System.Reflection;
using System.IO;
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

#pragma warning disable IDE0051 // Remove unused private members

namespace BossNotifier {
    [BepInPlugin("Mattdokn.BossNotifier", "BossNotifier", "2.0.1")]
    [BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
    public class BossNotifierPlugin : BaseUnityPlugin {
        public static PropertyInfo FikaIsPlayerHost;
        private static Type FikaIntegrationType;


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
            { WildSpawnType.bossPartisan, "Partisan" },
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
            Log(LogLevel.Info, "[BossNotifier] Awake() called");

            // Detect Fika EARLY to subscribe to network events before they fire
            Type FikaUtilExternalType = Type.GetType("Fika.Core.Main.Utils.FikaBackendUtils, Fika.Core", false);
            if (FikaUtilExternalType != null) {
                // Search for Fika.Core assembly
                System.Reflection.Assembly fikaAssembly = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                    if (assembly.GetName().Name == "Fika.Core") {
                        fikaAssembly = assembly;
                        break;
                    }
                }

                if (fikaAssembly != null) {
                    Type fikaBackendUtils = fikaAssembly.GetType("Fika.Core.Main.Utils.FikaBackendUtils");
                    if (fikaBackendUtils != null) {
                        FikaIsPlayerHost = AccessTools.Property(fikaBackendUtils, "ClientType");
                        Log(LogLevel.Info, "[BossNotifier] ✓ Fika detected in Awake! Initializing integration...");

                        try {
                            // Load BossNotifier.FikaOptional.dll.bin from same directory as main DLL
                            string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                            string fikaPath = Path.Combine(pluginDir, "BossNotifier.FikaOptional.dll.bin");

                            if (File.Exists(fikaPath)) {
                                Log(LogLevel.Info, $"[BossNotifier] Loading Fika integration from {fikaPath}");
                                Assembly fikaAsm = Assembly.LoadFrom(fikaPath);
                                FikaIntegrationType = fikaAsm.GetType("BossNotifier.FikaIntegration");

                                if (FikaIntegrationType != null) {
                                    MethodInfo initMethod = FikaIntegrationType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                                    if (initMethod != null) {
                                        initMethod.Invoke(null, null);
                                        Log(LogLevel.Info, "[BossNotifier] ✓ Fika integration initialized in Awake");
                                    } else {
                                        Log(LogLevel.Error, "[BossNotifier] Could not find Initialize method in FikaIntegration");
                                    }
                                } else {
                                    Log(LogLevel.Error, "[BossNotifier] Could not load FikaIntegration type from assembly");
                                }
                            } else {
                                Log(LogLevel.Info, "[BossNotifier] BossNotifier.FikaOptional.dll.bin not found, running in singleplayer mode");
                            }
                        } catch (Exception ex) {
                            Log(LogLevel.Error, $"[BossNotifier] Failed to initialize Fika integration: {ex}");
                        }
                    }
                }
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

        // Fika helper methods
        public static bool IsFikaInstalled() {
            bool installed = FikaIsPlayerHost != null;
            Log(LogLevel.Debug, $"[BossNotifier] IsFikaInstalled: {installed}");
            return installed;
        }

        public static bool IsHost() {
            if (!IsFikaInstalled()) {
                Log(LogLevel.Debug, "[BossNotifier] IsHost: false (Fika not installed)");
                return false;
            }
            int clientType = (int)FikaIsPlayerHost.GetValue(null);
            bool isHost = clientType == 2;  // 2 = Host
            Log(LogLevel.Debug, $"[BossNotifier] IsHost: {isHost} (ClientType: {clientType})");
            return isHost;
        }

        public static bool IsClient() {
            if (!IsFikaInstalled()) {
                Log(LogLevel.Debug, "[BossNotifier] IsClient: false (Fika not installed)");
                return false;
            }
            int clientType = (int)FikaIsPlayerHost.GetValue(null);
            bool isClient = clientType == 1;  // 1 = Client
            Log(LogLevel.Debug, $"[BossNotifier] IsClient: {isClient} (ClientType: {clientType})");
            return isClient;
        }

        public static bool IsSingleplayer() {
            bool isSP = !IsFikaInstalled();
            Log(LogLevel.Debug, $"[BossNotifier] IsSingleplayer: {isSP}");
            return isSP;
        }

        // Reflection-based helper methods to call FikaIntegration
        public static void SendVicinityNotificationToClients(string message) {
            if (FikaIntegrationType == null) {
                Log(LogLevel.Warning, "[BossNotifier] FikaIntegration type not loaded, cannot send vicinity notification");
                return;
            }

            try {
                MethodInfo method = FikaIntegrationType.GetMethod("SendVicinityNotificationToClients", BindingFlags.Public | BindingFlags.Static);
                if (method != null) {
                    method.Invoke(null, new object[] { message });
                } else {
                    Log(LogLevel.Error, "[BossNotifier] Could not find SendVicinityNotificationToClients method");
                }
            } catch (Exception ex) {
                Log(LogLevel.Error, $"[BossNotifier] Error calling SendVicinityNotificationToClients: {ex}");
            }
        }

        public static void SendBossInfoRequest() {
            if (FikaIntegrationType == null) {
                Log(LogLevel.Warning, "[BossNotifier] FikaIntegration type not loaded, cannot send boss info request");
                return;
            }

            try {
                MethodInfo method = FikaIntegrationType.GetMethod("SendBossInfoRequest", BindingFlags.Public | BindingFlags.Static);
                if (method != null) {
                    method.Invoke(null, null);
                } else {
                    Log(LogLevel.Error, "[BossNotifier] Could not find SendBossInfoRequest method");
                }
            } catch (Exception ex) {
                Log(LogLevel.Error, $"[BossNotifier] Error calling SendBossInfoRequest: {ex}");
            }
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
                BossNotifierPlugin.Log(LogLevel.Error, "Tried to add boss with null location.");
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
            try {
                // If the boss will spawn
                if (__instance.ShallSpawn) {
                    // Get it's name, if no name found then return.
                    string name = BossNotifierPlugin.GetBossName(__instance.BossType);
                    if (name == null) return;

                    // Get the spawn location
                    string location = BossNotifierPlugin.GetZoneName(__instance.BornZone);

                    BossNotifierPlugin.Log(LogLevel.Info, $"Boss {name} @ zone {__instance.BornZone} translated to {(location == null ? __instance.BornZone.Replace("Bot", "").Replace("Zone", ""): location)}");

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
            } catch (Exception ex) {
                BossNotifierPlugin.Log(LogLevel.Error, $"[BossNotifier] Error in BossLocationSpawnPatch: {ex}");
                throw;
            }
        }
    }

    // Patch for tracking live boss spawns
    internal class BotBossPatch : ModulePatch {
        protected override MethodBase GetTargetMethod() => typeof(BotBoss).GetConstructors()[0];

        // Bosses spawned in raid
        public static HashSet<string> spawnedBosses = new HashSet<string>();

        // Notification queue
        public static Queue<string> vicinityNotifications = new Queue<string>();

        [PatchPostfix]
        private static void PatchPostfix(BotBoss __instance) {
            try {
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

                // Create vicinity notification message
                string vicinityMessage = $"{name} {(BossNotifierPlugin.pluralBosses.Contains(name) ? "have" : "has")} been detected in your vicinity.";

                // Enqueue for local display
                vicinityNotifications.Enqueue(vicinityMessage);

                // If Fika is installed and we're the host, send to all clients
                if (BossNotifierPlugin.IsFikaInstalled() && BossNotifierPlugin.IsHost())
                {
                    BossNotifierPlugin.Log(LogLevel.Info, $"[BossNotifier] Host detected, sending vicinity notification to clients: {vicinityMessage}");
                    BossNotifierPlugin.SendVicinityNotificationToClients(vicinityMessage);
                }
            } catch (Exception ex) {
                BossNotifierPlugin.Log(LogLevel.Error, $"[BossNotifier] Error in BotBossPatch: {ex}");
                throw;
            }
        }
    }

    // Patch for hooking when a raid is started
    internal class NewGamePatch : ModulePatch {
        protected override MethodBase GetTargetMethod() => typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted));

        [PatchPostfix]
        public static void PatchPostfix() {
            try {
                // Start BossNotifierMono
                BossNotifierMono.Init();
            } catch (Exception ex) {
                BossNotifierPlugin.Log(LogLevel.Error, $"[BossNotifier] Error in NewGamePatch: {ex}");
                throw;
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
        public int intelCenterLevel;

        private void SendBossNotifications() {
            if (!ShouldFunction()) return;
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
            if (Singleton<GameWorld>.Instantiated) {
                Instance = Singleton<GameWorld>.Instance.GetOrAddComponent<BossNotifierMono>();
                BossNotifierPlugin.Log(LogLevel.Info, $"Game started on map {Singleton<GameWorld>.Instance.LocationId}");
                if (ClientAppUtils.GetMainApp().GetClientBackEndSession() == null) {
                    Instance.intelCenterLevel = 0;
                } else {
                    Instance.intelCenterLevel = ClientAppUtils.GetMainApp().GetClientBackEndSession().Profile.Hideout.Areas[11].Level;
                }
            }
        }

        public void Start() {
            BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] BossNotifierMono.Start() called");

            // Log Fika state for diagnostics (detection already done in Awake)
            BossNotifierPlugin.Log(LogLevel.Info, $"[BossNotifier] Fika installed: {BossNotifierPlugin.IsFikaInstalled()}");
            if (BossNotifierPlugin.IsFikaInstalled()) {
                int clientType = (int)BossNotifierPlugin.FikaIsPlayerHost.GetValue(null);
                string clientTypeStr = clientType == 0 ? "None" : (clientType == 1 ? "Client" : (clientType == 2 ? "Host" : "Unknown"));
                BossNotifierPlugin.Log(LogLevel.Info, $"[BossNotifier] ClientType at Start(): {clientType} ({clientTypeStr})");
            }

            GenerateBossNotifications();
            BossNotifierPlugin.Log(LogLevel.Debug, "[BossNotifier] After GenerateBossNotifications, checking config...");
            BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] showNotificationsOnRaidStart = {BossNotifierPlugin.showNotificationsOnRaidStart.Value}");

            if (!BossNotifierPlugin.showNotificationsOnRaidStart.Value) {
                BossNotifierPlugin.Log(LogLevel.Warning, "[BossNotifier] Auto-notifications disabled in config!");
                return;
            }

            // Use retry mechanism to handle Fika initialization timing
            BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] Starting automatic notification scheduling with retries...");
            StartCoroutine(TryScheduleAutomaticNotification());
        }

        // Coroutine to retry scheduling automatic notifications until Fika is ready
        private System.Collections.IEnumerator TryScheduleAutomaticNotification()
        {
            int maxRetries = 10;
            int retryCount = 0;
            float retryInterval = 0.5f; // Check every 0.5 seconds

            while (retryCount < maxRetries)
            {
                BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] Attempt {retryCount + 1}/{maxRetries} to determine client/host mode...");
                BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier]   IsClient() = {BossNotifierPlugin.IsClient()}");
                BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier]   IsHost() = {BossNotifierPlugin.IsHost()}");

                if (BossNotifierPlugin.IsClient())
                {
                    // Client: Request boss info after delay
                    BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] ✓ Client mode detected: Scheduling boss info request in 3.5s");
                    Invoke("RequestBossInfoFromHost", 3.5f);
                    yield break; // Success, exit coroutine
                }
                else if (BossNotifierPlugin.IsHost())
                {
                    // Host: Show after 2 seconds
                    BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] ✓ Host mode detected: Scheduling notifications in 2s");
                    Invoke("SendBossNotifications", 2f);
                    yield break; // Success, exit coroutine
                }
                else if (!BossNotifierPlugin.IsFikaInstalled())
                {
                    // Singleplayer: Show after 2 seconds
                    BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] ✓ Singleplayer mode detected: Scheduling notifications in 2s");
                    Invoke("SendBossNotifications", 2f);
                    yield break; // Success, exit coroutine
                }

                retryCount++;
                BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] Client/Host mode not determined yet, waiting {retryInterval}s...");
                yield return new UnityEngine.WaitForSeconds(retryInterval);
            }

            // If we get here, we failed to determine mode after all retries
            BossNotifierPlugin.Log(LogLevel.Warning, $"[BossNotifier] Failed to determine client/host mode after {maxRetries} retries, defaulting to singleplayer behavior");
            Invoke("SendBossNotifications", 2f);
        }

        public void Update() {
            if (BotBossPatch.vicinityNotifications.Count > 0) {
                string notif = BotBossPatch.vicinityNotifications.Dequeue();
                if (Instance.intelCenterLevel >= BossNotifierPlugin.intelCenterDetectedUnlockLevel.Value) {
                    NotificationManagerClass.DisplayMessageNotification(notif, ENotificationDurationType.Long);
                    Instance.GenerateBossNotifications();
                }
            }

            if (IsKeyPressed(BossNotifierPlugin.showBossesKeyCode.Value)) {
                BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] Hotkey pressed!");
                if (BossNotifierPlugin.IsClient()) {
                    // Client: Request from host
                    BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] Client mode: Requesting boss info from host");
                    RequestBossInfoFromHost();
                } else {
                    // Host/Singleplayer: Show directly
                    BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] Host/SP mode: Showing notifications directly");
                    SendBossNotifications();
                }
            }
        }

        public void OnDestroy() {
            // Clear out boss locations for this raid
            BossLocationSpawnPatch.bossesInRaid.Clear();
            // Clear out spawned bosses for this raid
            BotBossPatch.spawnedBosses.Clear();
        }

        public bool ShouldFunction() {
            // NEW: Always return true (both host and client can function now)
            // Clients will request data from host via packets
            BossNotifierPlugin.Log(LogLevel.Debug, "[BossNotifier] ShouldFunction: true (clients now supported)");
            return true;
        }

        public void GenerateBossNotifications() {
            // Clear out boss notification cache
            bossNotificationMessages = new List<string>();

            // Check if it's daytime to prevent showing Cultist notif (with null checks for Fika clients)
            bool isDayTime = false;
            try {
                if (Singleton<IBotGame>.Instance != null &&
                    Singleton<IBotGame>.Instance.BotsController != null &&
                    Singleton<IBotGame>.Instance.BotsController.ZonesLeaveController != null)
                {
                    isDayTime = Singleton<IBotGame>.Instance.BotsController.ZonesLeaveController.IsDay();
                }
            } catch (Exception ex) {
                BossNotifierPlugin.Log(LogLevel.Warning, $"[BossNotifier] Could not determine day/night in GenerateBossNotifications: {ex.Message}");
            }

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
                BossNotifierPlugin.Log(LogLevel.Info, notificationMessage);
                // Add notification to cache list
                bossNotificationMessages.Add(notificationMessage);
            }
        }

        // Client method: Request boss info from host
        private void RequestBossInfoFromHost() {
            BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] RequestBossInfoFromHost() called");

            if (!BossNotifierPlugin.IsClient()) {
                BossNotifierPlugin.Log(LogLevel.Warning, "[BossNotifier] RequestBossInfoFromHost called but not a client!");
                return;
            }

            // Call into FikaIntegration via reflection to avoid loading Fika types when not installed
            BossNotifierPlugin.SendBossInfoRequest();
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
