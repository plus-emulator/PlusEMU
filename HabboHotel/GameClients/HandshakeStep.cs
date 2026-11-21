namespace Plus.HabboHotel.GameClients;

[Flags]
internal enum HandshakeStep
{
    UniqueId = 1,
    InitDiffieHandshake = 2,
    CompleteDiffieHandshake = 4
}
