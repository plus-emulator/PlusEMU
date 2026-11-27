-- Mystery box prizes are the Christmas 2012 furni line. The fireplace is the rare prize.
-- Furniture resolves by exact class name, and a missing name is skipped. Idempotent.

DROP TEMPORARY TABLE IF EXISTS mystery_box_prize_seed;
CREATE TEMPORARY TABLE mystery_box_prize_seed (
    item_name VARCHAR(70) COLLATE utf8mb4_bin PRIMARY KEY,
    weight INT NOT NULL
) DEFAULT CHARSET=utf8mb4;
INSERT INTO mystery_box_prize_seed VALUES
    ('xmas12_floortile',5),('xmas12_bardesk',5),('xmas12_pork',5),('xmas12_nutcracker',5),('xmas12_table',5),
    ('xmas12_santathrone',5),('xmas12_npole',5),('xmas12_chair',5),('xmas12_pillar1',5),('xmas12_pillar2',5),
    ('xmas12_pillar3',5),('xmas12_barrel',5),('xmas12_snack',5),('xmas12_floortile2',5),('xmas12_cfloor',5),
    ('xmas12_worktable',5),('xmas12_cfloorbig',5),('xmas12_wall_in',5),('xmas12_window',5),('xmas12_wall_out',5),
    ('xmas12_door',5),('xmas12_torch',5),('xmas12_fireplace',1);

INSERT INTO mystery_box_prizes (furniture_id, weight)
SELECT f.id, s.weight
FROM mystery_box_prize_seed s
JOIN furniture f ON BINARY f.item_name = s.item_name
WHERE NOT EXISTS (SELECT 1 FROM mystery_box_prizes p WHERE p.furniture_id = f.id)
ORDER BY f.id;

DROP TEMPORARY TABLE mystery_box_prize_seed;
