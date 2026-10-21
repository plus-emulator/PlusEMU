-- Before effect boxes had their own interaction, using one only switched it to state 1. State 1 is the opening animation, and its
-- last frames hide the box (AIR furniture_effectbox with the fxbox assets), so such a box stays invisible, cannot be clicked and still
-- takes its tile. Habbo removes a box once it is opened, so no box rests in state 1: those boxes close again and their owners can open them.
-- Idempotent: reruns change nothing.

UPDATE items i
JOIN furniture f ON f.id = i.base_item
SET i.extra_data = '0'
WHERE f.interaction_type = 'effect_box' AND i.extra_data = '1';
