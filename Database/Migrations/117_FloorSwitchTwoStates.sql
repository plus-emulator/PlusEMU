-- The floor switch (wf_floor_switch1) has two states, as in Habbo: clicks and Toggle to Random give 0 and 1. It had three.
-- Idempotent: reruns end in the same state.

UPDATE furniture SET interaction_modes_count = 2
WHERE item_name = 'wf_floor_switch1' AND interaction_modes_count <> 2;
