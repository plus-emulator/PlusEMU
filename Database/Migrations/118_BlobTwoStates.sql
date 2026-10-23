-- The blobs (wf_blob, wf_blob2) have two states, as in Habbo: Toggle State turns them between 0 and 1. They had four.
-- Idempotent: reruns end in the same state.

UPDATE furniture SET interaction_modes_count = 2
WHERE item_name IN ('wf_blob', 'wf_blob2') AND interaction_modes_count <> 2;
