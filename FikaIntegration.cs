using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Comfort.Common;
using Fika.Core.Coop.Utils;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using BossNotifier.Packets;
using LiteNetLib;

namespace BossNotifier
{
    // Static class to handle all Fika-related functionality
    public static class FikaIntegration
    {
        // Register Fika event handlers
        public static void Initialize()
        {
            BossNotifierPlugin.Log(LogLevel.Debug, "FikaIntegration.Initialize called");
            // Register Fika packet handler using Fika's event system by calling new Action to avoid errors when fika
            // is not present. Thanks Tyfon for this tip.
            FikaEventDispatcher.SubscribeEvent(new Action<FikaNetworkManagerCreatedEvent>(OnFikaNetworkManagerCreated));
            BossNotifierPlugin.Log(LogLevel.Debug, "Subscribed to FikaNetworkManagerCreated event");
        }

        // Handler for Fika network manager creation event
        private static void OnFikaNetworkManagerCreated(FikaNetworkManagerCreatedEvent evt)
        {
            BossNotifierPlugin.Log(LogLevel.Debug, "OnFikaNetworkManagerCreated event received");
            // Only register if this is a client
            if (IsClient())
            {
                BossNotifierPlugin.Log(LogLevel.Debug, "IsClient is true");
                var netMan = evt.Manager as FikaClient;
                if (!netMan) return;
                BossNotifierPlugin.Log(LogLevel.Debug, "FikaClient instance found, registering packet");
                netMan.RegisterPacket(new Action<BossListPacket>(OnBossListPacket));
                BossNotifierPlugin.Log(LogLevel.Info, "Registered BossListPacket handler for Fika client via event.");
                netMan.RegisterPacket(new Action<VicinityNotificationPacket>(OnVicinityNotificationPacket));
                BossNotifierPlugin.Log(LogLevel.Info, "Registered VicinityNotificationPacket handler for Fika client via event.");
            }
        }

        // Called when a BossListPacket is received from the host
        private static void OnBossListPacket(BossListPacket packet)
        {
            BossNotifierPlugin.Log(LogLevel.Debug, $"OnBossListPacket called with {packet.BossNames.Count} bosses");
            BossNotifierPlugin.Log(LogLevel.Info, "Received BossListPacket from host.");
            BossNotifierPlugin.Log(LogLevel.Info, $"Bosses in raid: {string.Join(", ", packet.BossNames)}");
            
            BossLocationSpawnPatch.bossesInRaid.Clear();
            for (int i = 0; i < packet.BossNames.Count; i++)
            {
                BossNotifierPlugin.Log(LogLevel.Debug, $"Boss: {packet.BossNames[i]}, Location: {packet.Locations[i]}");
                BossLocationSpawnPatch.bossesInRaid[packet.BossNames[i]] = packet.Locations[i];
            }
            
            // Regenerate notifications if the mono instance exists
            if (BossNotifierMono.Instance)
            {
                BossNotifierMono.Instance.GenerateBossNotifications();
            }
        }

        // Called when a VicinityNotificationPacket is received from the host
        private static void OnVicinityNotificationPacket(VicinityNotificationPacket packet)
        {
            BossNotifierPlugin.Log(LogLevel.Debug, $"OnVicinityNotificationPacket called with message: {packet.Message}");
            // Enqueue the message for display on the client
            BotBossPatch.vicinityNotifications.Enqueue(packet.Message);
        }

        // Send the boss list to all clients (called from host)
        public static void SendBossListToClients(Dictionary<string, string> bossesInRaid)
        {
            var netMan = Singleton<FikaServer>.Instance;
            if (netMan)
            {
                BossNotifierPlugin.Log(LogLevel.Debug, "FikaServer instance found, sending BossListPacket to all clients");
                var packet = new BossListPacket
                {
                    BossNames = new List<string>(),
                    Locations = new List<string>()
                };
                foreach (var kvp in bossesInRaid)
                {
                    packet.BossNames.Add(kvp.Key);
                    packet.Locations.Add(kvp.Value);
                }
                netMan.SendDataToAll(ref packet, DeliveryMethod.ReliableOrdered);
            }
            else
            {
                BossNotifierPlugin.Log(LogLevel.Debug, "FikaServer instance not found, skipping packet send");
            }
        }

        // Send a vicinity notification to all clients (called from host)
        public static void SendVicinityNotificationToClients(string message)
        {
            var netMan = Singleton<FikaServer>.Instance;
            if (netMan)
            {
                BossNotifierPlugin.Log(LogLevel.Debug, $"FikaServer instance found, sending VicinityNotificationPacket to all clients: {message}");
                var packet = new VicinityNotificationPacket { Message = message };
                netMan.SendDataToAll(ref packet, DeliveryMethod.ReliableOrdered);
            }
            else
            {
                BossNotifierPlugin.Log(LogLevel.Debug, "FikaServer instance not found, skipping vicinity notification packet send");
            }
        }

        public static bool IsClient()
        {
            return FikaBackendUtils.IsClient;
        }

        public static bool IsHost()
        {
            return FikaBackendUtils.IsServer;
        }
    }
}

