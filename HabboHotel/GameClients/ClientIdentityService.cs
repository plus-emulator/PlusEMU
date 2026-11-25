using Plus.Communication.Packets.Outgoing.Handshake;
using Plus.HabboHotel.Moderation;

namespace Plus.HabboHotel.GameClients;

public interface IClientIdentityService
{
    void SetMachineIdentity(GameClient session, string machineId);
}

public sealed class ClientIdentityService(IModerationManager moderation) : IClientIdentityService
{
    // A machine ban stores at most 50 characters. A longer id is ignored as if the client sent none.
    private const int MaxMachineIdLength = 50;

    public void SetMachineIdentity(GameClient session, string machineId)
    {
        if (machineId.Length > MaxMachineIdLength || session.IsAuthenticated || !session.TryStartHandshakeStep(HandshakeStep.UniqueId)) {
            return;
        }

        session.RecordMachineId(machineId);

        if (moderation.HasBan(ModerationBanType.Machine, machineId)) {
            session.Disconnect();

            return;
        }

        session.Send(new SetUniqueIdComposer(machineId));
    }
}
