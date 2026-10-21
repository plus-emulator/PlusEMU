using System.Collections.Immutable;
using Plus.Communication.Packets.Outgoing.Inventory.AvatarEffect;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Users.Effects;

// Prepared scalars for the outgoing effect packets; composers only write these values.
public sealed record AvatarEffectActivation(int SpriteId, int Duration, bool Permanent = false);

public sealed record AvatarEffectExpiry(int SpriteId);

public sealed record AvatarEffectEntry(int SpriteId, int Duration, int Quantity, bool Activated, int RemainingSeconds, bool Permanent = false);

public interface IAvatarEffectService
{
    // Looks up, checks and activates against one captured instant; the activation persists before the packet is sent.
    void Activate(GameClient session, int effectId);

    // A negative id or 0 takes the effect off; any other id must be an owned, active effect. Outside a room the choice is kept for the next room entry.
    void Select(GameClient session, int effectId);

    // The full effects list at one captured instant, with remaining time measured against that instant.
    ImmutableArray<AvatarEffectEntry> Capture(Habbo habbo);
}

public sealed class AvatarEffectService(TimeProvider time) : IAvatarEffectService
{
    public void Activate(GameClient session, int effectId)
    {
        var habbo = session.GetHabbo();
        var now = time.GetUtcNow();

        if (habbo.Effects is not { } effects || effects.GetEffectNullableAt(effectId, now, false, true) is not { } effect || effects.HasEffectAt(effectId, now, true)) {
            return;
        }

        effect.Activate(now);
        session.Send(new AvatarEffectActivatedComposer(new AvatarEffectActivation(effect.SpriteId, ToWire(effect.Duration))));
    }

    public void Select(GameClient session, int effectId)
    {
        var selected = effectId < 0 ? 0 : effectId;
        var habbo = session.GetHabbo();

        if (habbo.Effects is not { } effects || selected != 0 && !effects.HasEffectAt(selected, time.GetUtcNow(), true)) {
            return;
        }

        if (habbo.CurrentRoom?.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id) is { } user) {
            user.ApplyEffect(selected);
        }
        else {
            // The room entry puts the kept effect on.
            effects.CurrentEffect = selected;
        }
    }

    public ImmutableArray<AvatarEffectEntry> Capture(Habbo habbo)
    {
        if (habbo.Effects is not { } effects) {
            return [];
        }

        var now = time.GetUtcNow();

        return effects.GetAllEffects
            .Select(effect => new AvatarEffectEntry(effect.SpriteId, ToWire(effect.Duration), effect.Activated ? effect.Quantity - 1 : effect.Quantity,
                effect.Activated, effect.Activated ? ToWire(effect.TimeLeftAt(now)) : -1))
            .ToImmutableArray();
    }

    // Wire fields are 32-bit: values are truncated like the original casts, then held inside [0, int.MaxValue].
    private static int ToWire(double value) => (int)Math.Clamp(value, 0, int.MaxValue);
}
