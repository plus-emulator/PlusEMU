-- The Colour Tile (wf_colortile) is an ordinary 7-state furni on Habbo, not a pressure pad: Wired "toggle state" cycles it
-- 0..6 inside the flash ObjectsDataUpdate, a user's use cycles it with an ObjectDataUpdate, and walking on it changes nothing
-- (observed on hhus, AIR WIN63-202610131318-860371703). Idempotent.

UPDATE `furniture` SET `interaction_type` = 'default', `interaction_modes_count` = 7
WHERE `item_name` = 'wf_colortile' AND `interaction_type` = 'pressure_pad';
