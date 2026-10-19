-- Sells every named pet companion (the "pt" figure sets) on a Companions page under Clothing, right after Accessories.
-- The companions already exist as purchasable_clothing furni, but no page sold them, so the wardrobe's pet tab stayed empty.
-- None has an official offer (furnidata offerid -1), so each gets a new 25 credit offer from the custom id range.
-- An item whose furniture row is missing is skipped.
-- Idempotent: reruns end in the same state.

START TRANSACTION;

-- Make room after Accessories, only when the page is new.
UPDATE catalog_pages s
JOIN catalog_pages clothing ON clothing.id = s.parent_id AND clothing.link = 'clothing' AND clothing.catalog_mode = 'NORMAL'
JOIN catalog_pages accessories ON accessories.parent_id = clothing.id AND accessories.link = 'clothing_accessories'
LEFT JOIN catalog_pages companions ON companions.link = 'clothing_companions' AND companions.catalog_mode = 'NORMAL'
SET s.position = s.position + 1
WHERE s.position > accessories.position AND companions.id IS NULL;

INSERT INTO catalog_pages (parent_id, caption, icon, position, link, layout, catalog_mode, visible, enabled, required_club_level)
SELECT clothing.id, 'Companions', 74, accessories.position + 1, 'clothing_companions', 'default_3x3', 'NORMAL', 1, 1, 0
FROM catalog_pages clothing
JOIN catalog_pages accessories ON accessories.parent_id = clothing.id AND accessories.link = 'clothing_accessories'
WHERE clothing.link = 'clothing' AND clothing.catalog_mode = 'NORMAL'
  AND NOT EXISTS (SELECT 1 FROM catalog_pages c WHERE c.link = 'clothing_companions' AND c.catalog_mode = 'NORMAL');

INSERT IGNORE INTO catalog_page_images (page_id, slot, image)
SELECT c.id, i.slot, i.image FROM catalog_pages c
JOIN (SELECT 0 AS slot, 'clothing_catalog_header' AS image UNION ALL SELECT 1, '' UNION ALL SELECT 2, '') i
WHERE c.link = 'clothing_companions' AND c.catalog_mode = 'NORMAL';

INSERT IGNORE INTO catalog_page_texts (page_id, slot, text)
SELECT c.id, t.slot, t.text FROM catalog_pages c
JOIN (SELECT 0 AS slot, 'Pick a little friend to keep you company. To wear one, place it in your room and double click it to add it to your wardrobe.' AS text
    UNION ALL SELECT 1, '' UNION ALL SELECT 2, '') t
WHERE c.link = 'clothing_companions' AND c.catalog_mode = 'NORMAL';

-- Newest first.
CREATE TEMPORARY TABLE pet_companion_plan_102 (
    item_name VARCHAR(70) NOT NULL PRIMARY KEY,
    position INT NOT NULL,
    furniture_id INT UNSIGNED NULL,
    offer_id INT NULL
) ENGINE=MEMORY;

INSERT INTO pet_companion_plan_102 (item_name, position) VALUES
    ('clothing_nftballoonbuddy', 0), ('hween_cloth_r26_possessedpumpkin', 1), ('hween_cloth_c26_tinyowl', 2),
    ('hween_cloth_c26_raven', 3), ('hween_cloth_c26_hauntedpuppet', 4), ('hween_cloth_c26_cat', 5),
    ('hween_cloth_c26_batfriend', 6), ('clothing_toastfriend', 7), ('clothing_scallywagseagull', 8),
    ('clothing_adorablepetshark', 9), ('clothing_nftlittlemonkey', 10), ('clothing_r26_toucancompanion', 11),
    ('clothing_nftsquirrel', 12), ('clothing_r26_wisp', 13), ('clothing_r26_mysticdragon', 14),
    ('clothing_r26_robotcompanion', 15), ('clothing_r26_minielephant', 16), ('clothing_nftshoulderdragon1', 17),
    ('clothing_r25_yellowaisha', 18), ('clothing_r25_shroomie', 19), ('clothing_blessingofhades', 20),
    ('clothing_r25_unicorncompanion', 21), ('clothing_nftmonkeyfriend', 22), ('clothing_nftsnake', 23),
    ('clothing_r25_bunnycompanion', 24), ('clothing_nftcat', 25), ('clothing_teddycompanion', 26),
    ('clothing_r25_goosefriend', 27), ('clothing_nftbabysnowman', 28), ('clothing_nftbabyrudolf', 29),
    ('clothing_r24_ghostfriend', 30), ('clothing_nftsummerbird', 31), ('clothing_nftshiba', 32);

UPDATE pet_companion_plan_102 p SET p.furniture_id = (
    SELECT MIN(f.id) FROM furniture f WHERE f.item_name = p.item_name AND f.interaction_type = 'purchasable_clothing');

-- On a rerun the offer is the one already on the page; other offers with the same key (for example Builders Club) stay apart.
UPDATE pet_companion_plan_102 p SET p.offer_id = (
    SELECT MIN(po.offer_id) FROM catalog_page_offers po
    JOIN catalog_pages c ON c.id = po.page_id AND c.link = 'clothing_companions' AND c.catalog_mode = 'NORMAL'
    JOIN catalog_offer_products op ON op.offer_id = po.offer_id AND op.product_type = 'furni' AND op.furniture_id = p.furniture_id)
WHERE p.furniture_id IS NOT NULL;

-- New offers get ids above every existing one, which is how they are told apart from older offers with the same key.
SET @pet_companion_offers_before_102 = (SELECT COALESCE(MAX(id), 0) FROM catalog_offers);
INSERT INTO catalog_offers (localization_key, cost_credits, cost_points, points_type, club_level, bulk_purchase, enabled, preview_image)
SELECT p.item_name, 25, 0, 0, 0, 1, 1, ''
FROM pet_companion_plan_102 p
WHERE p.offer_id IS NULL AND p.furniture_id IS NOT NULL
  AND EXISTS (SELECT 1 FROM catalog_pages c WHERE c.link = 'clothing_companions' AND c.catalog_mode = 'NORMAL')
ORDER BY p.position;
UPDATE pet_companion_plan_102 p JOIN catalog_offers o ON o.id > @pet_companion_offers_before_102 AND o.localization_key = p.item_name
SET p.offer_id = o.id WHERE p.offer_id IS NULL AND p.furniture_id IS NOT NULL;

INSERT INTO catalog_offer_products (offer_id, position, product_type, furniture_id, amount, extra_param)
SELECT p.offer_id, 0, 'furni', p.furniture_id, 1, ''
FROM pet_companion_plan_102 p
WHERE p.offer_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM catalog_offer_products op WHERE op.offer_id = p.offer_id);

INSERT IGNORE INTO catalog_page_offers (page_id, offer_id, position)
SELECT c.id, p.offer_id, p.position
FROM pet_companion_plan_102 p JOIN catalog_pages c ON c.link = 'clothing_companions' AND c.catalog_mode = 'NORMAL'
WHERE p.offer_id IS NOT NULL;

DROP TEMPORARY TABLE pet_companion_plan_102;

COMMIT;
