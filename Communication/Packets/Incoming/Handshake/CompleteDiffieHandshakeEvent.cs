using Plus.Communication.Attributes;
using Plus.Communication.Encryption;
using Plus.Communication.Packets.Outgoing.Handshake;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Handshake;

[NoAuthenticationRequired]
public class CompleteDiffieHandshakeEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var cipherPublickey = packet.ReadString();
        var sharedKey = HabboEncryptionV2.CalculateDiffieHellmanSharedKey(cipherPublickey);

        if (sharedKey != 0) {
            // Sent before RC4 starts, so it goes out in the clear like the client expects.
            session.Send(new SecretKeyComposer(HabboEncryptionV2.GetRsaDiffieHellmanPublicKey(), session.EncryptsOutgoingTraffic));
            session.ActivateLegacyCrypto(HabboEncryptionV2.Rc4KeyFromSharedKey(sharedKey));
        }
        else {
            session.SendNotification("There was an error logging you in, please try again!");
        }

        return Task.CompletedTask;
    }
}
