-- Habbo's Glass Door has two states, 0 (closed) and 1 (open) (hof_furni/50950/wf_glassdoor visualization).
UPDATE furniture SET interaction_modes_count = 2 WHERE item_name = 'wf_glassdoor';

UPDATE items i JOIN furniture f ON f.id = i.base_item
SET i.extra_data = '0'
WHERE f.item_name = 'wf_glassdoor' AND i.extra_data NOT IN ('0', '1');
