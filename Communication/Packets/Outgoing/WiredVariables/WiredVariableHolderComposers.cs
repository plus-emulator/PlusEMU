using System.Globalization;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Variables;

namespace Plus.Communication.Packets.Outgoing.WiredVariables;

public enum WiredMenuError : short
{
    Failed = 1,
    NoHolders = 2,
    NoOwners = 3,
    UnknownEntity = 4,
    OwnerOnly = 7
}

public sealed class WiredMenuErrorComposer(WiredMenuError error) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredMenuErrorComposer;
    public void Compose(IOutgoingPacket packet) => packet.WriteShort((short)error);
}

public sealed class WiredSetUserPermanentVariableResultComposer(bool success) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredSetUserPermanentVariableResultComposer;
    public void Compose(IOutgoingPacket packet) => packet.WriteBoolean(success);
}

/// <summary>The holders in the room of one variable: kind (1 furni, 2 user), the variable, then holder id and value pairs.</summary>
public sealed class WiredAllVariableHoldersComposer(WiredVariableDescription variable, IReadOnlyList<(int EntityId, long Value)> holders) : IServerPacket
{
    private readonly WiredVariableDescription _variable = WiredVariableWireCapture.Capture(variable);
    private readonly (int EntityId, long Value)[] _holders = [.. holders];
    public uint MessageId => ServerPacketHeader.WiredAllVariableHoldersComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_variable.Definition.Target == WiredVariableTarget.Furni ? 1 : 2);
        WriteVariable(packet, _variable);
        packet.WriteInteger(_holders.Length);

        foreach (var (entityId, value) in _holders) {
            packet.WriteInteger(entityId);
            packet.WriteInteger(WiredVariableHolderWire.Clamp(value));
        }
    }

    // A holder variable is a custom furni or user variable: user created (type 0), interceptable, furni target 0, user target 1.
    private static void WriteVariable(IOutgoingPacket packet, WiredVariableDescription variable)
    {
        packet.WriteString(variable.CatalogId);
        packet.WriteInteger(0);
        packet.WriteString(variable.Definition.Name);
        packet.WriteInteger(variable.WireAvailability);
        packet.WriteInteger(variable.Definition.Target == WiredVariableTarget.Furni ? 0 : 1);
        packet.WriteBoolean(false);
        packet.WriteBoolean(variable.CanCreateAndDelete);
        packet.WriteBoolean(variable.HasValue);
        packet.WriteBoolean(variable.CanWriteValue);
        packet.WriteBoolean(true);
        packet.WriteBoolean(false);
        packet.WriteBoolean(variable.RequiresExecutionContext);
        packet.WriteBoolean(variable.CanReadCreationTime);
        packet.WriteBoolean(variable.CanReadLastUpdateTime && variable.HasValue);
        WiredAllVariablesDiffComposer.WriteTextConnector(packet, variable);
    }
}

/// <summary>One owners page of a permanent user variable. Every row is a Habbo: entity type 1 and the user id.</summary>
public sealed class WiredUserVariablesListComposer(string variableId, WiredVariableHolderPage page, int userType, int sort) : IServerPacket
{
    private readonly WiredVariableHolderPage _page = page with { Holders = [.. page.Holders] };
    public uint MessageId => ServerPacketHeader.WiredUserVariablesListComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(variableId);
        packet.WriteInteger(_page.Total);
        packet.WriteInteger(_page.Page);
        packet.WriteInteger(_page.PageSize);
        packet.WriteInteger(_page.Holders.Count);

        foreach (var holder in _page.Holders) {
            packet.WriteInteger(1);
            packet.WriteInteger(checked((int)holder.Key.HolderId));
            packet.WriteString(holder.Name);
            packet.WriteInteger(WiredVariableHolderWire.Clamp(holder.Value.Value));
            WiredVariableHolderWire.WriteTime(packet, holder.Value.CreatedAt);
            WiredVariableHolderWire.WriteTime(packet, holder.Value.UpdatedAt);
        }

        packet.WriteInteger(userType);
        packet.WriteInteger(sort);
    }
}

/// <summary>The permanent variables one Habbo holds, with the Habbo's name and figure.</summary>
public sealed class WiredUserPermanentVariablesComposer(int userId, string name, string figure,
    IReadOnlyList<(WiredVariableDescription Variable, WiredVariableValue Value)> values) : IServerPacket
{
    private readonly (WiredVariableDescription Variable, WiredVariableValue Value)[] _values = [.. values];
    public uint MessageId => ServerPacketHeader.WiredUserPermanentVariablesComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(1);
        packet.WriteInteger(userId);
        packet.WriteString(name);
        packet.WriteString(figure);
        packet.WriteInteger(_values.Length);

        foreach (var (variable, value) in _values) {
            packet.WriteString(variable.CatalogId);
            packet.WriteInteger(WiredVariableHolderWire.Clamp(value.Value));
            WiredVariableHolderWire.WriteTime(packet, value.CreatedAt);
            WiredVariableHolderWire.WriteTime(packet, value.UpdatedAt);
        }
    }
}

internal static class WiredVariableHolderWire
{
    public static int Clamp(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

    // Epoch milliseconds, then the same moment as UTC text.
    public static void WriteTime(IOutgoingPacket packet, DateTimeOffset? time)
    {
        var milliseconds = time?.ToUnixTimeMilliseconds() ?? 0;
        packet.WriteInteger(unchecked((int)(milliseconds >> 32)));
        packet.WriteInteger(unchecked((int)milliseconds));
        packet.WriteString(time is { } value ? value.UtcDateTime.ToString("dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture) : "");
    }
}
