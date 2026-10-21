-- The wired disabler, the roller speed controller and the hand item blocker get their own interactions, as the Invisible Furni
-- Controller and the :chooser disabler have. The wired disabler has two states (on and off), like the other switches.
-- Only rows that still use a default interaction change, so a custom interaction is kept. Idempotent: reruns end in the same state.

UPDATE furniture SET interaction_type = 'wired_disable', interaction_modes_count = 2
WHERE item_name = 'conf_wired_disable' AND type = 's' AND interaction_type IN ('default', 'default_floor', 'wired_disable');
UPDATE furniture SET interaction_type = 'queue_speed', interaction_modes_count = 12
WHERE item_name = 'conf_queue_speed' AND type = 's' AND interaction_type IN ('default', 'default_floor', 'queue_speed');
UPDATE furniture SET interaction_type = 'handitem_block', interaction_modes_count = 2
WHERE item_name = 'conf_handitem_block' AND type = 's' AND interaction_type IN ('default', 'default_floor', 'handitem_block');
