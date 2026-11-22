using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Catalog;
using Plus.Communication.Packets.Outgoing.Moderation;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Groups;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms.Chat.Filter;
using Plus.Utilities;
using Plus.Utilities.DependencyInjection;

namespace Plus.HabboHotel.Catalog;

[Singleton]
public interface ICatalogFurniExtraData
{
    // The extra data a bought piece of furniture starts with; null when the purchase must be refused.
    string? Read(GameClient session, CatalogProduct product, string extraData, DateTimeOffset utcNow);

    // Text a buyer writes, escaped, cut at maxLength and word filtered unless the buyer may bypass the filter.
    string ReadText(GameClient session, string text, int maxLength, bool allowBreaks);
}

public sealed class CatalogFurniExtraData : ICatalogFurniExtraData
{
    // The catalogue's trophy inscription box takes 300 characters.
    private const int MaxTrophyTextLength = 300;

    private readonly IGroupManager _groups;
    private readonly IWordFilterManager _wordFilter;
    private readonly ILogger<CatalogFurniExtraData> _logger;

    public CatalogFurniExtraData(IGroupManager groups, IWordFilterManager wordFilter, ILogger<CatalogFurniExtraData> logger)
    {
        _groups = groups;
        _wordFilter = wordFilter;
        _logger = logger;
    }

    // Bought furniture of any other type starts with an empty string.
    public static bool HasPurchaseData(InteractionType type) => type is InteractionType.GuildItem or InteractionType.GuildGate
        or InteractionType.GuildForum or InteractionType.Floor or InteractionType.Wallpaper or InteractionType.Landscape
        or InteractionType.Recolorable or InteractionType.Postit or InteractionType.Moodlight or InteractionType.Trophy
        or InteractionType.Mannequin or InteractionType.BadgeDisplay;

    public string? Read(GameClient session, CatalogProduct product, string extraData, DateTimeOffset utcNow)
    {
        var definition = product.Definition!;

        if (!HasPurchaseData(definition.InteractionType)) {
            return "";
        }

        switch (definition.InteractionType) {
            case InteractionType.GuildItem:
            case InteractionType.GuildGate:
            case InteractionType.GuildForum:
                return ReadGuildExtraData(session, definition.InteractionType, extraData);
            case InteractionType.Floor:
            case InteractionType.Wallpaper:
            case InteractionType.Landscape:
                // Each pattern offer names its pattern; the client sends an empty value for those.
                if (!string.IsNullOrEmpty(product.ExtraParam)) {
                    return product.ExtraParam;
                }

                double number = 0;

                try {
                    number = string.IsNullOrEmpty(extraData) ? 0 : double.Parse(extraData, CultureInfo.InvariantCulture);
                }
                catch (Exception e) {
                    _logger.LogWarning(e, "Invalid catalog floor extra data {ExtraData}", extraData);
                }

                return number.ToString(CultureInfo.InvariantCulture); // maintain extra data // todo: validate
            case InteractionType.Recolorable:
                // The client sends the chosen "r,g,b" and only offers the class's palette;
                // any other value starts in the palette's first colour.
                return RecolorableDataFormat.FormatCatalogueColor(RecolorablePalette.PurchaseColor(definition.ItemName,
                    RecolorableDataFormat.TryParseCatalogueColor(extraData, out var chosen) ? chosen : null));
            case InteractionType.Postit:
                return "FFFF33";
            case InteractionType.Moodlight:
                return "1,1,1,#000000,255";
            case InteractionType.Trophy:
                return $"{session.GetHabbo().Username}{Convert.ToChar(9)}{utcNow.Day}-{utcNow.Month}-{utcNow.Year}{Convert.ToChar(9)}{ReadText(session, extraData, MaxTrophyTextLength, allowBreaks: true)}";
            case InteractionType.Mannequin:
                return $"m{Convert.ToChar(5)}.ch-210-1321.lg-285-92{Convert.ToChar(5)}Default Mannequin";
            case InteractionType.BadgeDisplay:
                if (session.GetHabbo().Inventory?.Badges.HasBadge(extraData) != true) {
                    session.Send(new BroadcastMessageAlertComposer("Oops, it appears that you do not own this badge."));

                    return null;
                }

                return $"{extraData}{Convert.ToChar(9)}{session.GetHabbo().Username}{Convert.ToChar(9)}{utcNow.Day}-{utcNow.Month}-{utcNow.Year}";
            default:
                throw new UnreachableException($"No purchase data rule for {definition.InteractionType}.");
        }
    }

    public string ReadText(GameClient session, string text, int maxLength, bool allowBreaks)
    {
        text = StringCharFilter.Escape(text, allowBreaks);

        if (text.Length > maxLength) {
            text = text[..maxLength];
        }

        return session.GetHabbo().Access.Can(PermissionKeys.ChatFilterBypass) ? text : _wordFilter.CheckMessage(text);
    }

    // GuildSelectorCatalogWidget sends the chosen group's id. Group furniture needs a group the buyer belongs to. A forum
    // terminal for a group without a forum opens that forum, which only its owner may do: GuildForumSelectorCatalogWidget
    // offers the groups that have a forum and the groups the user owns.
    private string? ReadGuildExtraData(GameClient session, InteractionType type, string extraData)
    {
        var habboId = session.GetHabbo().Id;

        if (int.TryParse(extraData, NumberStyles.None, CultureInfo.InvariantCulture, out var groupId) && _groups.TryGetGroup(groupId, out var group)
            && (type == InteractionType.GuildForum && !group.ForumEnabled ? group.CreatorId == habboId : group.CreatorId == habboId || group.IsMember(habboId))) {
            return groupId.ToString(CultureInfo.InvariantCulture);
        }

        session.Send(new PurchaseErrorComposer(PurchaseError.Rejected));

        return null;
    }
}
