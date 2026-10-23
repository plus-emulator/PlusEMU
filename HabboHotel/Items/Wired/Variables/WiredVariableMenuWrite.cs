namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>A decoded and bounded variable write from the editor; packet framing stays in the handler.</summary>
public sealed record WiredVariableMenuWrite(int Action, WiredVariableTarget Target, int TargetId, uint DefinitionId, long Value, string Token);

/// <summary>
/// An inspection-tab write: the variable's client target (0 furni, 1 user, -10 global), the furni id, user room index or 0,
/// the catalog variable id, the value and the operation (0 set, 1 create, 2 delete).
/// </summary>
public sealed record WiredObjectVariableWrite(int Target, int ObjectId, string VariableId, int Value, int Operation);
