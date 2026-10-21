-- Effect boxes (fxbox_fx<N>) get their own interaction: the owner opens one once, it leaves the room and gives avatar effect N for good
-- (AIR furniture_effectbox logic). Only rows that still use a default interaction change, so a custom interaction is kept.
-- Idempotent: reruns end in the same state.

UPDATE furniture SET interaction_type = 'effect_box'
WHERE item_name LIKE 'fxbox\_fx%' AND type = 's' AND interaction_type IN ('default', 'default_floor', 'effect_box');
