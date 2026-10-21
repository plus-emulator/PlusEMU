-- The small Builders Club block is 0.25 lower per state, as in Habbo (1.0, 0.75, 0.5, 0.25 and 0.0 for states 0 to 4), so a state
-- change also changes its height.
-- Idempotent: reruns end in the same state.

UPDATE furniture SET height_adjustable = '1.0,0.75,0.5,0.25,0.0'
WHERE item_name = 'bc_block_0_recolorable' AND height_adjustable <> '1.0,0.75,0.5,0.25,0.0';
