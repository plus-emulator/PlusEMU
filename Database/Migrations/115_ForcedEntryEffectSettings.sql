-- Staff (moderation.tool) and ambassadors wear an effect when they enter a room. These settings choose the effect id hotel wide,
-- and 0 turns it off for everyone with that role, staff hired later included. Users can still turn it off for themselves with
-- :forced_effects. Idempotent: reruns keep a value that was already set.

INSERT IGNORE INTO server_settings (`key`, `value`) VALUES ('room.forced_effect.staff', '102'), ('room.forced_effect.ambassador', '178');
