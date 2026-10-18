namespace Plus.Communication.Packets.Outgoing;

/// <summary>
/// A message only our own web client understands. Sessions on the plain Flash/AIR socket do not get it, so their wire matches the
/// official hotel's.
/// </summary>
public interface IClientExtensionPacket;
