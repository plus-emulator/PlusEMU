using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Links;

/// <summary>One dataframe row as sent: its key, the row's text and, for number rows, the number.</summary>
public sealed record WiredLinkRow(string Key, string Text, long? Number);

/// <summary>
/// A link message (rows empty) or dataframe travelling between WIRED Link endpoints. Hops counts the link deliveries that led to
/// it, so stacks answering each other across rooms stop after <see cref="WiredLinkTransport.MaxHops"/>.
/// </summary>
public sealed record WiredLinkPayload(uint SourceRoomId, uint SourceItemId, string Message, ImmutableArray<WiredLinkRow> Rows, int Hops);

/// <summary>
/// Server-side link delivery. A message sent through a WIRED Link furni reaches every other furni of its network in loaded rooms.
/// A link that receives local messages also receives its own, with or without a network. Rooms that are not loaded do not receive it.
/// </summary>
public static class WiredLinkTransport
{
    public const int MaxHops = 9;
    public const string Interaction = "wired_link";

    public static bool IsLink(Item item) => string.Equals(item.Definition.InteractionName, Interaction, StringComparison.OrdinalIgnoreCase);

    /// <summary>Sends through each picked endpoint; returns whether any receiving furni was offered the event.</summary>
    public static bool Send(Room room, IEnumerable<Item> endpoints, WiredLinkPayload payload, WiredEventKind kind, IWiredLinkStore store, IRoomManager? rooms)
    {
        if (payload.Hops > MaxHops) {
            return false;
        }

        var delivered = false;
        var reached = new HashSet<uint>();

        foreach (var endpoint in endpoints.Where(IsLink).DistinctBy(item => item.Id)) {
            if (store.Endpoint(endpoint.Id) is not { } joined) {
                continue;
            }

            if (joined.ReceiveLocal && reached.Add(endpoint.Id)) {
                delivered |= Deliver(room, Event(endpoint, endpoint));
            }

            if (joined.NetworkId == 0) {
                continue;
            }

            foreach (var member in store.Members(joined.NetworkId)) {
                if (member.ItemId == endpoint.Id || !reached.Add(member.ItemId) || member.RoomId == room.Id && !member.ReceiveLocal) {
                    continue;
                }

                var target = member.RoomId == room.Id ? room : rooms != null && rooms.TryGetRoom(member.RoomId, out var loaded) ? loaded : null;

                if (target?.GetRoomItemHandler()?.GetItem(member.ItemId) is not { } receiver || !IsLink(receiver)) {
                    continue;
                }

                var @event = Event(endpoint, receiver);

                // The sending room's engine is locked while its effect runs; another room is handed the event on the thread pool,
                // so two rooms sending to each other never wait on each other's lock. That room starts a pass at once, which switches the link state, shows the bubble and runs the stacks, in that order.
                if (ReferenceEquals(target, room)) {
                    delivered |= Deliver(room, @event);
                }
                else {
                    room.GetWired().HandOff(() => Deliver(target, @event, true));
                    delivered = true;
                }
            }
        }

        return delivered;

        static bool Deliver(Room target, WiredRuntimeEvent @event, bool startPass = false)
        {
            var wired = target.GetWired();

            if (startPass) {
                wired?.DispatchNow(@event);

                return wired != null;
            }

            wired?.SpeakLink(@event);

            return wired?.Dispatch(@event) ?? false;
        }

        WiredRuntimeEvent Event(Item sender, Item receiver) => new(kind)
        {
            EventItem = receiver,
            Message = payload.Message,
            Link = payload with { SourceItemId = sender.Id }
        };
    }
}
