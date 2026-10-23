using Plus.Communication.Attributes;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;
using Plus.HabboHotel.Permissions;

namespace Plus.Communication.Packets.Incoming.Moderation;

[RequiresPermission(PermissionKeys.ModerationBanSoft)]
internal class ModerationBanEvent : IPacketEvent
{
    private readonly IModerationSanctionService _sanctions;

    public ModerationBanEvent(IModerationSanctionService sanctions)
    {
        _sanctions = sanctions;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        var message = packet.ReadString();
        packet.ReadInt(); // topic id
        var sanctionTypeId = packet.ReadInt();
        packet.ReadBool(); // avatar-only; accounts hold a single avatar here
        // An optional issue id follows.

        if (!BanHours.TryGetValue(sanctionTypeId, out var hours)) {
            return;
        }

        await _sanctions.Ban(session, new(userId, message, hours, false, false));
    }

    // Ban sanction types the moderation tool offers.
    internal static readonly IReadOnlyDictionary<int, int> BanHours = new Dictionary<int, int>
    {
        [3] = 18,
        [4] = 7 * 24,
        [5] = 30 * 24,
        [7] = 30 * 24,
        [6] = 100 * 365 * 24,
    };
}
