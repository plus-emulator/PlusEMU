-- The wired glowball has two states (animations 0 and 1), as in Habbo. It had four, so a double click went through two states that do not exist.
-- Idempotent: reruns end in the same state.

UPDATE furniture SET interaction_modes_count = 2
WHERE item_name = 'wf_glowball' AND interaction_modes_count <> 2;
