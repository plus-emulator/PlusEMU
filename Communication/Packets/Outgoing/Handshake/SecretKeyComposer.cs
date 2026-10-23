using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Handshake;

/// <summary>
/// The server's signed Diffie-Hellman public key, then whether the server encrypts what it sends. The client
/// reads the boolean only when bytes remain and keys its incoming RC4 only when it is true.
/// </summary>
public class SecretKeyComposer : IServerPacket
{
    private readonly string _publicKey;
    private readonly bool _serverClientEncryption;
    public uint MessageId => ServerPacketHeader.SecretKeyComposer;

    public SecretKeyComposer(string publicKey, bool serverClientEncryption)
    {
        _publicKey = publicKey;
        _serverClientEncryption = serverClientEncryption;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(_publicKey);
        packet.WriteBoolean(_serverClientEncryption);
    }
}
