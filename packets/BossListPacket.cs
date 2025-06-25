using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;

namespace BossNotifier.Packets
{
    // Packet for synchronizing boss list between host and clients
    public struct BossListPacket : INetSerializable
    {
        public List<string> BossNames;
        public List<string> Locations;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(BossNames?.Count ?? 0);
            if (BossNames != null && Locations != null)
            {
                for (int i = 0; i < BossNames.Count; i++)
                {
                    writer.Put(BossNames[i]);
                    writer.Put(Locations[i]);
                }
            }
        }

        public void Deserialize(NetDataReader reader)
        {
            if (BossNames == null) BossNames = new List<string>();
            if (Locations == null) Locations = new List<string>();
            BossNames.Clear();
            Locations.Clear();
            int count = reader.GetInt();
            for (int i = 0; i < count; i++)
            {
                BossNames.Add(reader.GetString());
                Locations.Add(reader.GetString());
            }
        }
    }
}

