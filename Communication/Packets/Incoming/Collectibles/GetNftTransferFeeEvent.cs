using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Collectibles;

/// <summary>The Transfer tab asks for the silver fee (AIR TransferNftsTab.as:201).</summary>
internal sealed class GetNftTransferFeeEvent(ICollectiblesHubService hub) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => hub.SendTransferFee(session);
}
