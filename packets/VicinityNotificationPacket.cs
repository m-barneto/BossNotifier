using LiteNetLib;
using LiteNetLib.Utils;

namespace BossNotifier.Packets
{
    // Packet for synchronizing vicinity notifications between host and clients
    public struct VicinityNotificationPacket : INetSerializable
    {
        public string Message;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Message);
        }

        public void Deserialize(NetDataReader reader)
        {
            Message = reader.GetString();
        }
    }
}

