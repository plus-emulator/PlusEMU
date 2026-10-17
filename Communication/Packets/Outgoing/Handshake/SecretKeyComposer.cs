using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Handshake;

/// <summary>
/// The server's signed Diffie-Hellman public key, then whether the server encrypts what it sends. The AIR client
/// reads the boolean only when bytes remain and keys its incoming RC4 only when it is true
/// (AIR WIN63-202610151745-318700621, package_30/class_4247.as:24-31, communication/demo/class_1974.as:208-212).
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
