using Plus.HabboHotel.Items.Wired.Configuration;

namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>Writes the explicit room value a definition save may carry, through normal authorization and change events.</summary>
public sealed class WiredVariableEditor(WiredVariableModule variables)
{
    /// <summary>After a definition configuration save, persist its explicit room value through normal authorization and change events.</summary>
    public bool SaveGlobalValue(uint definitionId, long value) => variables.SaveGlobalValue(definitionId, value);

    /// <summary>The refusal key for a new save of a variable name the room does not allow, or null.</summary>
    public string? RefuseName(uint itemId, string name, bool pointer) => variables.RefuseName(itemId, name, pointer);

    /// <summary>The refusal key for a pointer accessor the room's variables do not allow, or null.</summary>
    public string? RefuseAccessor(WiredVariableDefinition proposed) => variables.RefuseAccessor(proposed);

    /// <summary>True when the reference names a pointer variable.</summary>
    public bool IsPointer(WiredVariableReference reference) => variables.IsPointer(reference);

    /// <summary>The refusal key for a second reference to a shared variable a room already references, or null.</summary>
    public string? RefuseReference(WiredVariableDefinition proposed) => variables.RefuseReference(proposed);
}
