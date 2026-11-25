namespace Plus.HabboHotel.Items.DataFormat;

/// <summary>
/// A badge display: state, badge code, owner name and date. The client reads the badge from the second value.
/// It is stored as the badge code, owner name and date separated by tabs.
/// </summary>
public sealed class BadgeDisplayDataFormat : StringArrayDataFormat
{
    public override string Serialize() => string.Join('\t', Data.Skip(1));

    public override void Store(string data)
    {
        Data.Clear();
        Data.Add("0");
        Data.AddRange(data.Split('\t'));
        RaiseDataUpdated();
    }
}
