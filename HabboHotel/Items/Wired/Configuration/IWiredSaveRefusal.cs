namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>Rules that hold for a new editor save only. A stored row that loads is never checked against them.</summary>
public interface IWiredSaveRefusal
{
    /// <summary>The key the save is refused with, or null when it may be saved.</summary>
    string? RefuseSave(WiredConfiguration proposed);
}
