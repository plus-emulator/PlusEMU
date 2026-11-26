-- Ingredients are listed in recipe order, which is not the furniture id order.
ALTER TABLE crafting_recipes_ingredients ADD COLUMN IF NOT EXISTS position TINYINT UNSIGNED NOT NULL DEFAULT 0 AFTER amount;

-- Only crafting tables answer the crafting packets, and using one must not toggle its state.
UPDATE furniture SET interaction_type = 'crafting_table'
WHERE interaction_type = 'default'
  AND (BINARY item_name IN (
        'crafting_jarhearts','easter_c23_bunnycraftbot','easter_c24_craftingtable','fantasy_c22_craftsman','fest_c19_craft',
        'graffiti_c24_craftingtable','hween_c15_altar','hween_c19_witchcraft','hween_c21_scrollaltar','hween_c23_bastetcraft',
        'india_c20_sewingmachine','jungle_c16_worktable','plushie_c20_fluffmachine','stellar_c23_mooncraftingtable','tokyo_c18_craftbot',
        'xmas_c19_snowcraft','xmas_c20_crafter','xmas_c22_penguincrafting','xmas_c22_reindeercrafting','xmas_c23_chocolatecraftingtable',
        'survival_c26_craftingtable')
    OR id IN (SELECT altar_item_id FROM crafting_altars_recipes));

-- Recipes resolve their furniture by exact class name and sprite id. Existing codes are kept.
-- Missing or ambiguous furniture leaves a recipe unseeded.
DROP TEMPORARY TABLE IF EXISTS crafting_parity_new,crafting_parity_ingredients,crafting_parity_recipes;
CREATE TEMPORARY TABLE crafting_parity_recipes (
    code VARCHAR(128) COLLATE utf8mb4_bin PRIMARY KEY,
    table_name VARCHAR(70) COLLATE utf8mb4_bin NOT NULL,
    table_sprite INT NOT NULL,
    reward_sprite INT NOT NULL,
    secret BOOLEAN NOT NULL
) DEFAULT CHARSET=utf8mb4;
INSERT INTO crafting_parity_recipes VALUES
    ('survival_c26_cave','survival_c26_craftingtable',19220,19219,FALSE),
    ('survival_c26_factionsymbols','survival_c26_craftingtable',19220,19223,FALSE),
    ('survival_c26_flare','survival_c26_craftingtable',19220,19225,FALSE),
    ('survival_c26_foliage','survival_c26_craftingtable',19220,19227,FALSE),
    ('survival_c26_foliagefloor','survival_c26_craftingtable',19220,19228,FALSE),
    ('survival_c26_hammock','survival_c26_craftingtable',19220,19230,FALSE),
    ('survival_c26_podium','survival_c26_craftingtable',19220,19233,FALSE),
    ('survival_c26_stream','survival_c26_craftingtable',19220,19237,FALSE),
    ('survival_c26_surveillancetree','survival_c26_craftingtable',19220,19240,FALSE),
    ('survival_c26_trap','survival_c26_craftingtable',19220,19241,FALSE),
    ('survival_c26_treebridge','survival_c26_craftingtable',19220,19242,FALSE),
    ('survival_cloth_c26_braid','survival_c26_craftingtable',19220,19246,FALSE),
    ('survival_cloth_c26_drone','survival_c26_craftingtable',19220,19248,FALSE),
    ('survival_cloth_c26_survivaljumpsuit','survival_c26_craftingtable',19220,19250,FALSE),
    ('survival_cloth_c26_survivalmask','survival_c26_craftingtable',19220,19251,FALSE),
    ('xmas_c19_angelfigure2','xmas_c19_snowcraft',10911,10934,FALSE),
    ('xmas_c19_dragonfigure2','xmas_c19_snowcraft',10911,10915,FALSE),
    ('xmas_c19_reindeerfigure2','xmas_c19_snowcraft',10911,10897,FALSE),
    ('xmas_c19_robinfigure2','xmas_c19_snowcraft',10911,10933,FALSE),
    ('xmas_c19_unicornfigure2','xmas_c19_snowcraft',10911,10929,FALSE),
    ('clothing_firehelm','hween_c15_altar',8388,8342,FALSE),
    ('clothing_airhelm','hween_c15_altar',8388,8340,FALSE),
    ('clothing_waterhelm','hween_c15_altar',8388,8336,FALSE),
    ('hween_c15_purecrystal2','hween_c15_altar',8388,8363,TRUE),
    ('hween_c15_purecrystal3','hween_c15_altar',8388,8373,TRUE),
    ('hween_c15_evilcrystal2','hween_c15_altar',8388,8362,TRUE),
    ('hween_c15_evilcrystal3','hween_c15_altar',8388,8365,TRUE),
    ('hween14_mariachi','hween_c15_altar',8388,6179,TRUE),
    ('hween14_doll3','hween_c15_altar',8388,6203,TRUE),
    ('guitar_skull','hween_c15_altar',8388,4011,TRUE),
    ('hween14_skelepieces','hween_c15_altar',8388,6178,TRUE),
    ('hween13_bldtrail','hween_c15_altar',8388,5300,TRUE),
    ('penguin_glow','hween_c15_altar',8388,2993,TRUE),
    ('skullcandle','hween_c15_altar',8388,207,TRUE),
    ('deadduck','hween_c15_altar',8388,208,TRUE);
CREATE TEMPORARY TABLE crafting_parity_ingredients (
    code VARCHAR(128) COLLATE utf8mb4_bin NOT NULL,
    item_name VARCHAR(70) COLLATE utf8mb4_bin NOT NULL,
    sprite_id INT NOT NULL,
    amount INT NOT NULL,
    position TINYINT UNSIGNED NOT NULL
) DEFAULT CHARSET=utf8mb4;
INSERT INTO crafting_parity_ingredients VALUES
    ('survival_c26_cave','survival_c26_stream',19237,1,1),
    ('survival_c26_cave','survival_c26_sleepingbag',19235,2,2),
    ('survival_c26_cave','survival_c26_foodsupplies',19229,1,3),
    ('survival_c26_factionsymbols','survival_c26_foodsupplies',19229,1,1),
    ('survival_c26_factionsymbols','survival_c26_stonesymbol',19236,1,2),
    ('survival_c26_flare','survival_c26_firekit',19224,2,1),
    ('survival_c26_foliage','survival_c26_firekit',19224,1,1),
    ('survival_c26_foliage','survival_c26_rope',19234,1,2),
    ('survival_c26_foliagefloor','survival_c26_foliage',19227,2,1),
    ('survival_c26_hammock','survival_c26_sleepingbag',19235,2,1),
    ('survival_c26_hammock','survival_c26_rope',19234,1,2),
    ('survival_c26_podium','survival_c26_foodsupplies',19229,1,1),
    ('survival_c26_podium','survival_c26_sleepingbag',19235,1,2),
    ('survival_c26_podium','survival_c26_stonesymbol',19236,1,3),
    ('survival_c26_stream','survival_c26_stonesymbol',19236,2,1),
    ('survival_c26_stream','survival_c26_sleepingbag',19235,1,2),
    ('survival_c26_surveillancetree','survival_c26_treebridge',19242,1,1),
    ('survival_c26_surveillancetree','survival_c26_factionsymbols',19223,1,2),
    ('survival_c26_trap','survival_c26_rope',19234,1,1),
    ('survival_c26_trap','survival_c26_foodsupplies',19229,1,2),
    ('survival_c26_treebridge','survival_c26_rope',19234,2,1),
    ('survival_c26_treebridge','survival_c26_sleepingbag',19235,1,2),
    ('survival_cloth_c26_braid','survival_c26_trap',19241,1,1),
    ('survival_cloth_c26_braid','survival_c26_podium',19233,1,2),
    ('survival_cloth_c26_braid','survival_c26_stonesymbol',19236,1,3),
    ('survival_cloth_c26_drone','survival_c26_surveillancetree',19240,1,1),
    ('survival_cloth_c26_drone','survival_c26_factionsymbols',19223,1,2),
    ('survival_cloth_c26_drone','survival_c26_foodsupplies',19229,2,3),
    ('survival_cloth_c26_survivaljumpsuit','survival_cloth_c26_survivalmask',19251,1,1),
    ('survival_cloth_c26_survivaljumpsuit','survival_c26_flare',19225,1,2),
    ('survival_cloth_c26_survivaljumpsuit','survival_c26_rope',19234,1,3),
    ('survival_cloth_c26_survivalmask','survival_c26_factionsymbols',19223,1,1),
    ('survival_cloth_c26_survivalmask','survival_c26_flare',19225,1,2),
    ('survival_cloth_c26_survivalmask','survival_c26_firekit',19224,1,3),
    ('xmas_c19_angelfigure2','xmas_c19_angelfigure',10884,5,1),
    ('xmas_c19_angelfigure2','xmas_c19_jarofgold',10881,2,2),
    ('xmas_c19_dragonfigure2','xmas_c19_dragonfigure',10930,3,1),
    ('xmas_c19_dragonfigure2','xmas_c19_jarofgold',10881,2,2),
    ('xmas_c19_reindeerfigure2','xmas_c19_reindeerfigure',10907,5,1),
    ('xmas_c19_robinfigure2','xmas_c19_robinfigure',10902,5,1),
    ('xmas_c19_unicornfigure2','xmas_c19_unicornfigure',10940,3,1),
    ('xmas_c19_unicornfigure2','xmas_c19_jarofgold',10881,2,2),
    ('clothing_firehelm','hween_c15_purecrystal3',8373,4,1),
    ('clothing_airhelm','hween_c15_purecrystal2',8363,4,1),
    ('clothing_airhelm','hween_c15_purecrystal3',8373,4,2),
    ('clothing_waterhelm','hween_c15_purecrystal2',8363,8,1),
    ('hween_c15_purecrystal2','hween_c15_purecrystal1',8404,3,1),
    ('hween_c15_purecrystal3','hween_c15_purecrystal2',8363,3,1),
    ('hween_c15_evilcrystal2','hween_c15_evilcrystal1',8370,3,1),
    ('hween_c15_evilcrystal3','hween_c15_evilcrystal2',8362,3,1),
    ('hween14_mariachi','hween14_skelepieces',6178,3,1),
    ('hween14_mariachi','studio_guitar',4083,1,2),
    ('hween14_mariachi','LT_skull',3189,1,3),
    ('hween14_mariachi','hween_c15_purecrystal2',8363,1,4),
    ('hween14_doll3','hween14_skelepieces',6178,3,1),
    ('hween14_doll3','duck_afro',5002,1,2),
    ('hween14_doll3','hween_c15_purecrystal2',8363,1,3),
    ('guitar_skull','rela_rock',3248,1,1),
    ('guitar_skull','duck_afro',5002,1,2),
    ('guitar_skull','hween_c15_purecrystal1',8404,1,3),
    ('hween14_skelepieces','deadduck',208,4,1),
    ('hween14_skelepieces','hween_c15_purecrystal1',8404,1,2),
    ('hween13_bldtrail','deadduck',208,2,1),
    ('hween13_bldtrail','hween_c15_purecrystal1',8404,1,2),
    ('penguin_glow','hween08_manhole',2946,1,1),
    ('penguin_glow','penguin_basic',2977,1,2),
    ('penguin_glow','hween_c15_evilcrystal2',8362,1,3),
    ('skullcandle','LT_skull',3189,1,1),
    ('skullcandle','rela_candle1',3238,1,2),
    ('deadduck','duck',179,1,1),
    ('deadduck','hween_c15_evilcrystal1',8370,1,2);
CREATE TEMPORARY TABLE crafting_parity_new AS
SELECT seed.code,reward.id AS reward_id,seed.secret,craft_table.id AS table_id
FROM crafting_parity_recipes seed
JOIN furniture reward ON BINARY reward.item_name=BINARY seed.code AND reward.sprite_id=seed.reward_sprite
JOIN furniture craft_table ON BINARY craft_table.item_name=BINARY seed.table_name AND craft_table.sprite_id=seed.table_sprite
WHERE (SELECT COUNT(*) FROM furniture item WHERE BINARY item.item_name=BINARY seed.code AND item.sprite_id=seed.reward_sprite)=1
  AND (SELECT COUNT(*) FROM furniture item WHERE BINARY item.item_name=BINARY seed.table_name AND item.sprite_id=seed.table_sprite)=1
  AND NOT EXISTS (SELECT 1 FROM crafting_recipes existing WHERE BINARY existing.code=BINARY seed.code)
  AND NOT EXISTS (
      SELECT 1 FROM crafting_parity_ingredients ingredient
      WHERE BINARY ingredient.code=BINARY seed.code
        AND (SELECT COUNT(*) FROM furniture item WHERE BINARY item.item_name=BINARY ingredient.item_name AND item.sprite_id=ingredient.sprite_id)<>1
  );
START TRANSACTION;
INSERT INTO crafting_recipes(code,product_code,reward_item_id,secret)
SELECT code,code,reward_id,secret FROM crafting_parity_new;
INSERT INTO crafting_altars_recipes(altar_item_id,recipe_id)
SELECT seed.table_id,recipe.id FROM crafting_parity_new seed JOIN crafting_recipes recipe ON BINARY recipe.code=BINARY seed.code;
INSERT INTO crafting_recipes_ingredients(recipe_id,item_id,amount,position)
SELECT recipe.id,item.id,ingredient.amount,ingredient.position
FROM crafting_parity_new seed
JOIN crafting_recipes recipe ON BINARY recipe.code=BINARY seed.code
JOIN crafting_parity_ingredients ingredient ON BINARY ingredient.code=BINARY seed.code
JOIN furniture item ON BINARY item.item_name=BINARY ingredient.item_name AND item.sprite_id=ingredient.sprite_id;
COMMIT;

-- Recipes that already exist get the Habbo ingredient order, and skeleton pieces take 4 dead ducks.
UPDATE crafting_recipes_ingredients line
JOIN crafting_recipes recipe ON recipe.id=line.recipe_id
JOIN furniture item ON item.id=line.item_id
JOIN crafting_parity_ingredients ingredient ON BINARY ingredient.code=BINARY recipe.code
    AND BINARY ingredient.item_name=BINARY item.item_name AND ingredient.sprite_id=item.sprite_id
SET line.position=ingredient.position,
    line.amount=IF(BINARY recipe.code=BINARY 'hween14_skelepieces' AND BINARY item.item_name=BINARY 'deadduck' AND line.amount=3, 4, line.amount);

DROP TEMPORARY TABLE crafting_parity_new,crafting_parity_ingredients,crafting_parity_recipes;
