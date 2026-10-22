-- The theater light (cine_light1) has three states, as in Habbo: clicks go 0, 1, 2 and back to 0. It had six, so clicks went through
-- three states that do not exist.
-- Idempotent: reruns end in the same state.

UPDATE furniture SET interaction_modes_count = 3
WHERE item_name = 'cine_light1' AND interaction_modes_count <> 3;
