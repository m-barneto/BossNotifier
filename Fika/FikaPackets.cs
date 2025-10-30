using Fika.Core.Networking.LiteNetLib.Utils;
using System.Collections.Generic;
using BepInEx.Logging;

namespace BossNotifier
{
    // Client → Host: Request boss information
    public class RequestBossInfoPacket : INetSerializable
    {
        // Empty packet - just signals request
        public void Serialize(NetDataWriter writer)
        {
            BossNotifierPlugin.Log(LogLevel.Debug, "[BossNotifier] [Packet] Serializing RequestBossInfoPacket");
        }

        public void Deserialize(NetDataReader reader)
        {
            BossNotifierPlugin.Log(LogLevel.Debug, "[BossNotifier] [Packet] Deserializing RequestBossInfoPacket");
        }
    }

    // Host → Client: Response with boss data
    public class BossInfoPacket : INetSerializable
    {
        public Dictionary<string, string> bossesInRaid;
        public HashSet<string> spawnedBosses;

        public void Serialize(NetDataWriter writer)
        {
            BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Packet] Serializing BossInfoPacket: {bossesInRaid?.Count ?? 0} bosses, {spawnedBosses?.Count ?? 0} spawned");

            // Serialize bossesInRaid
            if (bossesInRaid == null)
            {
                writer.Put(0);
            }
            else
            {
                writer.Put(bossesInRaid.Count);
                foreach (var kvp in bossesInRaid)
                {
                    writer.Put(kvp.Key);
                    writer.Put(kvp.Value);
                    BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Packet]   Boss: {kvp.Key} @ {kvp.Value}");
                }
            }

            // Serialize spawnedBosses
            if (spawnedBosses == null)
            {
                writer.Put(0);
            }
            else
            {
                writer.Put(spawnedBosses.Count);
                foreach (var boss in spawnedBosses)
                {
                    writer.Put(boss);
                    BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Packet]   Spawned: {boss}");
                }
            }
        }

        public void Deserialize(NetDataReader reader)
        {
            BossNotifierPlugin.Log(LogLevel.Debug, "[BossNotifier] [Packet] Deserializing BossInfoPacket");

            // Deserialize bossesInRaid
            int bossCount = reader.GetInt();
            bossesInRaid = new Dictionary<string, string>();
            BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Packet]   Reading {bossCount} bosses");
            for (int i = 0; i < bossCount; i++)
            {
                string key = reader.GetString();
                string value = reader.GetString();
                bossesInRaid[key] = value;
                BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Packet]     {key} @ {value}");
            }

            // Deserialize spawnedBosses
            int spawnedCount = reader.GetInt();
            spawnedBosses = new HashSet<string>();
            BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Packet]   Reading {spawnedCount} spawned bosses");
            for (int i = 0; i < spawnedCount; i++)
            {
                string boss = reader.GetString();
                spawnedBosses.Add(boss);
                BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Packet]     {boss}");
            }
        }
    }

    // Host → Clients: Vicinity notification when boss spawns during raid
    public class VicinityNotificationPacket : INetSerializable
    {
        public string message;

        public void Serialize(NetDataWriter writer)
        {
            BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Packet] Serializing VicinityNotificationPacket: {message}");
            writer.Put(message ?? "");
        }

        public void Deserialize(NetDataReader reader)
        {
            message = reader.GetString();
            BossNotifierPlugin.Log(LogLevel.Debug, $"[BossNotifier] [Packet] Deserializing VicinityNotificationPacket: {message}");
        }
    }
}
