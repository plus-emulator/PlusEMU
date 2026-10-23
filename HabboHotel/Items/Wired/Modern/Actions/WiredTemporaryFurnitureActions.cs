using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

public static class WiredTemporaryFurnitureActions
{
    public static bool Supports(string name) => name is "wf_act_place_furni" or "wf_act_remove_furni";
    public static WiredConfiguration Defaults(string name) => name == "wf_act_place_furni"
        ? new() { TemporaryPlacement = new() }
        : new() { IntParams = [0, 200] };

    public static bool Execute(string name, Item box, WiredRuntimeContext context, WiredConfiguration config, IItemDataManager definitions)
    {
        var handler = context.Room.GetRoomItemHandler();

        if (name == "wf_act_remove_furni") {
            // Both editor modes remove temporary identities, also copies a selector delivers. They never return or destroy a permanent
            // owned item.
            return handler.RemoveTemporaryFloorItemsByWired(
                context.Targets.ResolveFurni(context, config.SelectedItems, config.FurniSources["items"]).ToArray());
        }

        if (config.TemporaryPlacement is not { } policy) {
            return false;
        }

        var templateSource = config.FurniSources.GetValueOrDefault("templates", WiredSources.Selected);
        var templates = templateSource is WiredSources.Trigger or WiredSources.Selector or WiredSources.Signal
            ? DynamicTemplates(context, templateSource)
            : config.Snapshots;

        // A snapshot retains geometry and state, but its Builders Club template must still be in the room.
        var eligibleTemplates = templates.Select(snapshot => (Snapshot: snapshot,
            Definition: definitions.Items.GetValueOrDefault(snapshot.DefinitionId), Item: handler.GetItem(snapshot.ItemId)))
            .Where(template => template.Definition?.Type is Plus.HabboHotel.Users.Inventory.Furniture.ItemType.Floor or Plus.HabboHotel.Users.Inventory.Furniture.ItemType.Wall
                && template.Item is { } item && item.Definition.Id == template.Snapshot.DefinitionId && WiredPlaceTemplatePolicy.CanClone(item))
            .Select(template => (template.Snapshot, template.Definition)).ToArray();

        if (eligibleTemplates.Length == 0) {
            return false;
        }

        var (targetX, targetY, targetZ) = (0, 0, 0d);

        if (policy.Location == WiredPlaceLocationType.CustomLocation || policy.Altitude == WiredPlaceAltitudeType.CustomAltitude) {
            if (policy.TargetIsUser) {
                var user = context.Targets.ResolveUsers(context, [], config.UserSources.GetValueOrDefault("target", 0)).FirstOrDefault();

                if (user == null) {
                    return false;
                }

                (targetX, targetY, targetZ) = (user.X, user.Y, user.Z);
            }
            else {
                var targetSource = config.FurniSources.GetValueOrDefault("target", WiredSources.Selected);
                var picks = WiredNativeEditorProjection.UsesSecondary(config, "target", false)
                    ? config.SecondarySelectedItems
                    : config.SelectedItems;
                var target = context.Targets.ResolveFurni(context, picks, targetSource).FirstOrDefault();

                if (target == null) {
                    return false;
                }

                (targetX, targetY, targetZ) = (target.GetX, target.GetY, target.GetZ);
            }
        }

        var dx = policy.OffsetX;
        var dy = policy.OffsetY;

        if (policy.Location == WiredPlaceLocationType.CustomLocation) {
            dx += targetX - (eligibleTemplates[0].Snapshot.Wall?.TileX ?? eligibleTemplates[0].Snapshot.X);
            dy += targetY - (eligibleTemplates[0].Snapshot.Wall?.TileY ?? eligibleTemplates[0].Snapshot.Y);
        }

        long spawnValue = policy.Value;
        var valueTarget = (WiredVariableTarget)policy.ValueTarget;
        var module = policy.SpawnWithVariable ? context.Room.GetWired().Variables.Module : null;

        if (module != null && policy.ValueIsVariable) {
            // A value read from a variable is 0 whenever that variable cannot be read; the literal in the form is not a fallback.
            spawnValue = 0;

            if (context.VariableFrame is { } valueFrame && config.VariableIds.Length > 1 && Enum.IsDefined(valueTarget)
                && module.TryResolveCatalogId(config.VariableIds[1], valueTarget, out _)) {
                using var queries = new WiredVariableQueries(module, valueFrame);
                spawnValue = queries.ReadOperand(valueTarget, config.VariableIds[1],
                    config.UserSources.GetValueOrDefault("value", 0), config.FurniSources.GetValueOrDefault("value", 0), config) ?? 0;
            }
        }

        var placed = new List<Item>();
        var failedAny = false;

        foreach (var (snapshot, definition) in eligibleTemplates) {
            if (definition!.Type == Plus.HabboHotel.Users.Inventory.Furniture.ItemType.Wall) {
                if (snapshot.Wall is not { } wall) {
                    failedAny = true;

                    continue;
                }

                var model = context.Room.GetGameMap().StaticModel;
                var position = wall with { TileX = wall.TileX + dx, TileY = wall.TileY + dy };
                var wallHeight = policy.Altitude switch
                {
                    WiredPlaceAltitudeType.SourceAltitude => WiredWallGeometry.SavedAltitude(model, wall),
                    WiredPlaceAltitudeType.CustomAltitude => targetZ,
                    _ => context.Room.GetGameMap().ValidTile(position.TileX, position.TileY)
                        ? context.Room.GetGameMap().SqAbsoluteHeight(position.TileX, position.TileY) : 0
                };
                var altitude = Math.Max(0, wallHeight + policy.OffsetAltitudeHundredths / 100d);

                if (Plus.HabboHotel.Rooms.RoomItemHandling.AboveTemporaryLimit(altitude)) {
                    continue;
                }

                if (!WiredWallGeometry.TryProject(model, position, altitude, out var projected)
                    || handler.PlaceTemporaryWallItem(definition, box.OwnerId, projected.ToString(), snapshot.State) is not { } copy) {
                    failedAny = true;

                    continue;
                }

                placed.Add(copy);

                continue;
            }

            var x = snapshot.X + dx;
            var y = snapshot.Y + dy;

            if (!context.Room.GetGameMap().ValidTile(x, y)) {
                failedAny = true;

                continue;
            }

            var height = policy.Altitude switch
            {
                WiredPlaceAltitudeType.SourceAltitude => snapshot.Z,
                WiredPlaceAltitudeType.CustomAltitude => targetZ,
                _ => context.Room.GetGameMap().SqAbsoluteHeight(x, y)
            };
            var z = Math.Max(height + policy.OffsetAltitudeHundredths / 100d, 0);

            // Above the copy altitude limit nothing is placed and nothing is reported.
            if (Plus.HabboHotel.Rooms.RoomItemHandling.AboveTemporaryLimit(z)) {
                continue;
            }

            var item = handler.BlocksTemporaryCopy(definition!, x, y, snapshot.Rotation)
                ? null
                : handler.PlaceTemporaryFloorItem(definition!, box.OwnerId, x, y, snapshot.Rotation, z, snapshot.State, sendMessage: false);

            if (item == null) {
                failedAny = true;

                continue;
            }

            placed.Add(item);
        }

        // One Objects packet contains all successful copies from this firing, before their variable updates.
        context.Room.SendTemporaryFloorItems(placed.Where(item => item.IsFloorItem).ToArray());
        context.Room.SendTemporaryWallItems(placed.Where(item => item.IsWallItem).ToArray());

        foreach (var item in placed) {
            // A newly attached item is explicitly added to the child variable frame, never the firing's captured target sets.
            if (module != null && config.VariableIds.Length != 0 && context.VariableFrame is { } parent
                && module.TryResolveCatalogId(config.VariableIds[0], WiredVariableTarget.Furni, out var spawnVariable)) {
                var holder = WiredVariableRuntimeFrames.FurniHolder(item);
                var frame = new WiredVariableFrame(parent.RoomId, parent.Holders.Append(holder).ToArray())
                {
                    Context = parent.Context,
                    Trigger = parent.Trigger,
                    Signal = parent.Signal,
                    ResolveSource = parent.ResolveSource,
                    Depth = parent.Depth,
                    ChatText = parent.ChatText
                };
                frame.Selector.AddRange(parent.Selector);
                module.Mutate(spawnVariable, holder, WiredVariableMutation.Give, spawnValue, frame);
            }
        }

        // One report per run, however many copies failed; the room applies the dialog cooldown.
        if (failedAny) {
            context.Room.GetWired()?.ReportPlacementFailure(box);
        }

        return placed.Count != 0;
    }

    // Sources 0, 200 and 201 are read when the effect fires. Source 100 keeps the saved snapshots.
    private static IReadOnlyList<WiredFurniSnapshot> DynamicTemplates(WiredRuntimeContext context, int source) =>
        context.Targets.ResolveFurni(context, [], source)
            .Where(WiredPlaceTemplatePolicy.CanClone)
            .Select(WiredRoomOperations.Capture)
            .ToArray();
}
