namespace Plus.HabboHotel.GameClients;

public static class OutgoingPacketExtensions
{
    /// <summary>Writes a 4-byte big-endian IEEE-754 float, as the client's readFloat expects.</summary>
    public static void WriteFloat(this IOutgoingPacket packet, float value) => packet.WriteInteger(BitConverter.SingleToInt32Bits(value));
}
