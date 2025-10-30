using Fika.Core.Networking;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Comfort.Common;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using EFT.Communications;
using EFT;

namespace BossNotifier
{
    public static class FikaIntegration
    {
        public static void Initialize()
        {
            BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] [Fika] Initializing Fika integration...");

            try
            {
                // Subscribe to Fika network manager creation event
                FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerCreatedEvent>(OnNetworkManagerCreated);
                BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] [Fika] Subscribed to FikaNetworkManagerCreatedEvent");
            }
            catch (Exception ex)
            {
                BossNotifierPlugin.Log(LogLevel.Error, $"[BossNotifier] [Fika] Failed to subscribe to event: {ex}");
                throw;
            }
        }

        private static void OnNetworkManagerCreated(FikaNetworkManagerCreatedEvent e)
        {
            BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] [Fika] FikaNetworkManagerCreatedEvent received!");

            try
            {
                // Register packet handlers
                BossNotifierPlugin.Log(LogLevel.Debug, "[BossNotifier] [Fika] Registering RequestBossInfoPacket...");
                e.Manager.RegisterPacket<RequestBossInfoPacket, NetPeer>(OnRequestBossInfo);

                BossNotifierPlugin.Log(LogLevel.Debug, "[BossNotifier] [Fika] Registering BossInfoPacket...");
                e.Manager.RegisterPacket<BossInfoPacket>(OnBossInfoReceived);

                BossNotifierPlugin.Log(LogLevel.Debug, "[BossNotifier] [Fika] Registering VicinityNotificationPacket...");
                e.Manager.RegisterPacket<VicinityNotificationPacket>(OnVicinityNotificationReceived);

                BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] [Fika] Packets registered successfully!");
            }
            catch (Exception ex)
            {
                BossNotifierPlugin.Log(LogLevel.Error, $"[BossNotifier] [Fika] Error registering packets: {ex}");
                throw;
            }
        }

        // Handler: Client requests boss info (Host-side)
        private static void OnRequestBossInfo(RequestBossInfoPacket packet, NetPeer peer)
        {
            BossNotifierPlugin.Log(LogLevel.Info, $"[BossNotifier] [Fika] RequestBossInfo received from peer {peer?.Id ?? -1}");

            try
            {
                // Check if we're the host
                bool isHost = BossNotifierPlugin.IsHost();
                BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Fika]   IsHost: {isHost}");

                if (!isHost)
                {
                    BossNotifierPlugin.Log(LogLevel.Warning, "[BossNotifier] [Fika]   Non-host received request, ignoring");
                    return;
                }

                if (BossNotifierMono.Instance == null)
                {
                    BossNotifierPlugin.Log(LogLevel.Warning, "[BossNotifier] [Fika]   BossNotifierMono not initialized, cannot respond");
                    return;
                }

                BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Fika]   Preparing response...");
                BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Fika]     Bosses in raid: {BossLocationSpawnPatch.bossesInRaid.Count}");
                BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Fika]     Spawned bosses: {BotBossPatch.spawnedBosses.Count}");

                // Create response packet with current boss data
                BossInfoPacket response = new BossInfoPacket
                {
                    bossesInRaid = new Dictionary<string, string>(BossLocationSpawnPatch.bossesInRaid),
                    spawnedBosses = new HashSet<string>(BotBossPatch.spawnedBosses)
                };

                // Send response ONLY to the requesting peer
                var manager = Singleton<IFikaNetworkManager>.Instance;
                if (manager == null)
                {
                    BossNotifierPlugin.Log(LogLevel.Error, "[BossNotifier] [Fika]   Network manager is null, cannot send response");
                    return;
                }

                BossNotifierPlugin.Log(LogLevel.Info, $"[BossNotifier] [Fika]   Sending BossInfoPacket to peer {peer.Id}...");
                manager.SendDataToPeer(ref response, DeliveryMethod.ReliableOrdered, peer);
                BossNotifierPlugin.Log(LogLevel.Info, $"[BossNotifier] [Fika]   Response sent successfully!");
            }
            catch (Exception ex)
            {
                BossNotifierPlugin.Log(LogLevel.Error, $"[BossNotifier] [Fika] Error handling boss info request: {ex}");
                throw;
            }
        }

        // Handler: Host sends boss info (Client-side)
        private static void OnBossInfoReceived(BossInfoPacket packet)
        {
            BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] [Fika] BossInfoPacket received!");

            try
            {
                // Check if we're a client
                bool isClient = BossNotifierPlugin.IsClient();
                BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Fika]   IsClient: {isClient}");

                if (!isClient)
                {
                    BossNotifierPlugin.Log(LogLevel.Warning, "[BossNotifier] [Fika]   Non-client received BossInfoPacket, ignoring");
                    return;
                }

                if (BossNotifierMono.Instance == null)
                {
                    BossNotifierPlugin.Log(LogLevel.Warning, "[BossNotifier] [Fika]   BossNotifierMono not initialized, cannot display");
                    return;
                }

                BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Fika]   Packet contains {packet.bossesInRaid?.Count ?? 0} bosses, {packet.spawnedBosses?.Count ?? 0} spawned");

                // Process boss info directly (avoid passing Fika types to Plugin.cs)
                ProcessReceivedBossInfo(packet);
                BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] [Fika]   Boss info processed and displayed");
            }
            catch (Exception ex)
            {
                BossNotifierPlugin.Log(LogLevel.Error, $"[BossNotifier] [Fika] Error receiving boss info: {ex}");
                throw;
            }
        }

        // Client method: Send boss info request to host
        public static void SendBossInfoRequest()
        {
            BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] [Fika] SendBossInfoRequest() called");

            var manager = Singleton<IFikaNetworkManager>.Instance;
            if (manager == null)
            {
                BossNotifierPlugin.Log(LogLevel.Error, "[BossNotifier] [Fika] Network manager not available, cannot request boss info");
                return;
            }

            BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] [Fika] Sending RequestBossInfoPacket to host...");
            RequestBossInfoPacket request = new RequestBossInfoPacket();
            manager.SendData(ref request, DeliveryMethod.ReliableOrdered);
            BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] [Fika] Request sent!");
        }

        // Process received boss info from host (Client-side)
        private static void ProcessReceivedBossInfo(BossInfoPacket packet)
        {
            BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] [Fika] ProcessReceivedBossInfo() called");

            // Check intel center requirement (using client's own level)
            if (BossNotifierMono.Instance.intelCenterLevel < BossNotifierPlugin.intelCenterUnlockLevel.Value)
            {
                BossNotifierPlugin.Log(LogLevel.Info, $"[BossNotifier] [Fika] Intel center requirement not met ({BossNotifierMono.Instance.intelCenterLevel} < {BossNotifierPlugin.intelCenterUnlockLevel.Value})");
                return;
            }

            // If no bosses, show that
            if (packet.bossesInRaid == null || packet.bossesInRaid.Count == 0)
            {
                BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] [Fika] No bosses in raid");
                NotificationManagerClass.DisplayMessageNotification("No Bosses Located", ENotificationDurationType.Long);
                return;
            }

            // Display boss notifications from received data
            BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] [Fika] Displaying boss notifications from received data");
            DisplayBossNotificationsFromReceivedData(packet.bossesInRaid, packet.spawnedBosses);
        }

        // Display boss notifications from received packet data (Client-side)
        private static void DisplayBossNotificationsFromReceivedData(Dictionary<string, string> bosses, HashSet<string> spawned)
        {
            BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] [Fika] DisplayBossNotificationsFromReceivedData() called");

            // Use client's own intel center level (not host's)
            int intelLevel = BossNotifierMono.Instance.intelCenterLevel;

            // Check if it's daytime to prevent showing Cultist notif (with null checks)
            bool isDayTime = false;
            try {
                if (Singleton<IBotGame>.Instance != null &&
                    Singleton<IBotGame>.Instance.BotsController != null &&
                    Singleton<IBotGame>.Instance.BotsController.ZonesLeaveController != null)
                {
                    isDayTime = Singleton<IBotGame>.Instance.BotsController.ZonesLeaveController.IsDay();
                }
            } catch (Exception ex) {
                BossNotifierPlugin.Log(LogLevel.Warning, $"[BossNotifier] [Fika] Could not determine day/night: {ex.Message}");
            }
            BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Fika] Is daytime: {isDayTime}");

            // Get whether location is unlocked
            bool isLocationUnlocked = intelLevel >= BossNotifierPlugin.intelCenterLocationUnlockLevel.Value;
            BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Fika] Location unlocked: {isLocationUnlocked} ({intelLevel} >= {BossNotifierPlugin.intelCenterLocationUnlockLevel.Value})");

            // Get whether detection is unlocked
            bool isDetectionUnlocked = intelLevel >= BossNotifierPlugin.intelCenterDetectedUnlockLevel.Value;
            BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Fika] Detection unlocked: {isDetectionUnlocked} ({intelLevel} >= {BossNotifierPlugin.intelCenterDetectedUnlockLevel.Value})");

            int displayedCount = 0;
            foreach (var bossSpawn in bosses)
            {
                // If it's daytime then cultists don't spawn
                if (isDayTime && bossSpawn.Key.Equals("Cultists"))
                {
                    BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Fika] Skipping Cultists (daytime)");
                    continue;
                }

                // If boss has been spawned/detected
                bool isDetected = spawned != null && spawned.Contains(bossSpawn.Key);

                string notificationMessage;
                // If we don't have locations or value is null/whitespace
                if (!isLocationUnlocked || bossSpawn.Value == null || bossSpawn.Value.Equals(""))
                {
                    // Then just show that they spawned and nothing else
                    notificationMessage = $"{bossSpawn.Key} {(BossNotifierPlugin.pluralBosses.Contains(bossSpawn.Key) ? "have" : "has")} been located.{(isDetectionUnlocked && isDetected ? $" ✓" : "")}";
                }
                else
                {
                    // Location is unlocked and location isnt null
                    notificationMessage = $"{bossSpawn.Key} {(BossNotifierPlugin.pluralBosses.Contains(bossSpawn.Key) ? "have" : "has")} been located near {bossSpawn.Value}{(isDetectionUnlocked && isDetected ? $" ✓" : "")}";
                }

                BossNotifierPlugin.Log(LogLevel.Info, $"[BossNotifier] [Fika] Displaying: {notificationMessage}");
                NotificationManagerClass.DisplayMessageNotification(notificationMessage, ENotificationDurationType.Long);
                displayedCount++;
            }

            BossNotifierPlugin.Log(LogLevel.Info, $"[BossNotifier] [Fika] Displayed {displayedCount} boss notifications");
        }

        // Handler: Vicinity notification received (Client-side)
        private static void OnVicinityNotificationReceived(VicinityNotificationPacket packet)
        {
            BossNotifierPlugin.Log(LogLevel.Info, $"[BossNotifier] [Fika] VicinityNotificationPacket received: {packet.message}");

            try
            {
                // Check if we're a client
                bool isClient = BossNotifierPlugin.IsClient();
                BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Fika]   IsClient: {isClient}");

                if (!isClient)
                {
                    BossNotifierPlugin.Log(LogLevel.Warning, "[BossNotifier] [Fika]   Non-client received VicinityNotificationPacket, ignoring");
                    return;
                }

                // Enqueue the message for display
                BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Fika]   Enqueueing vicinity notification: {packet.message}");
                BotBossPatch.vicinityNotifications.Enqueue(packet.message);
            }
            catch (Exception ex)
            {
                BossNotifierPlugin.Log(LogLevel.Error, $"[BossNotifier] [Fika] Error receiving vicinity notification: {ex}");
                throw;
            }
        }

        // Host method: Send vicinity notification to all clients
        public static void SendVicinityNotificationToClients(string message)
        {
            BossNotifierPlugin.Log(LogLevel.Info, $"[BossNotifier] [Fika] SendVicinityNotificationToClients() called: {message}");

            var manager = Singleton<IFikaNetworkManager>.Instance;
            if (manager == null)
            {
                BossNotifierPlugin.Log(LogLevel.Error, "[BossNotifier] [Fika] Network manager not available, cannot send vicinity notification");
                return;
            }

            BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] [Fika] Sending VicinityNotificationPacket to all clients...");
            VicinityNotificationPacket packet = new VicinityNotificationPacket { message = message };
            manager.SendData(ref packet, DeliveryMethod.ReliableOrdered);
            BossNotifierPlugin.Log(LogLevel.Info, "[BossNotifier] [Fika] Vicinity notification sent!");
        }
    }
}
