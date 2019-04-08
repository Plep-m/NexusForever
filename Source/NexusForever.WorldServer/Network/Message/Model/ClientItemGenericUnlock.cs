using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Network.Message.Model.Shared;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ClientItemGenericUnlock, MessageDirection.Client)]
    public class ClientItemGenericUnlock : IReadable
    {
        public ItemLocation Location { get; } = new ItemLocation();

        public void Read(GamePacketReader reader)
        {
            Location.Read(reader);
        }
    }
}