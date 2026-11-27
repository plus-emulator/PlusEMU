-- Every user can buy the crafting supplies and the mystery box.
-- - The Tactical Supplies page moves from Staff to right after the Survivalist Crafting Table page and needs no permission.
-- - A Mystery Box page right after it lists the free mystery box offer.
-- Nothing changes when the Survivalist Crafting Table page is missing. Idempotent.

START TRANSACTION;

SET @craft_parent = (SELECT parent_id FROM catalog_pages WHERE link = 'nov26craft' AND catalog_mode = 'NORMAL');
SET @craft_position = (SELECT position FROM catalog_pages WHERE link = 'nov26craft' AND catalog_mode = 'NORMAL');
SET @supplies_move = @craft_parent IS NOT NULL AND EXISTS (
    SELECT 1 FROM catalog_pages WHERE link = 'nov26crackb' AND catalog_mode = 'NORMAL' AND NOT (parent_id <=> @craft_parent));

UPDATE catalog_pages SET position = position + 1
WHERE @supplies_move AND parent_id = @craft_parent AND position > @craft_position;

UPDATE catalog_pages SET parent_id = @craft_parent, position = @craft_position + 1
WHERE @supplies_move AND link = 'nov26crackb' AND catalog_mode = 'NORMAL';

UPDATE catalog_pages SET required_permission = NULL
WHERE link = 'nov26crackb' AND catalog_mode = 'NORMAL' AND parent_id <=> @craft_parent AND @craft_parent IS NOT NULL;

SET @box_offer = (
    SELECT MIN(o.id) FROM catalog_offers o
    JOIN catalog_offer_products p ON p.offer_id = o.id
    JOIN furniture f ON f.id = p.furniture_id
    WHERE f.item_name = 'mystery_box' AND o.cost_credits = 0 AND o.cost_points = 0);
SET @box_anchor = COALESCE(
    (SELECT position FROM catalog_pages WHERE link = 'nov26crackb' AND catalog_mode = 'NORMAL' AND parent_id <=> @craft_parent),
    @craft_position);
SET @box_add = @craft_parent IS NOT NULL AND @box_offer IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM catalog_pages WHERE link = 'xmas12_mysterybox' AND catalog_mode = 'NORMAL');

UPDATE catalog_pages SET position = position + 1
WHERE @box_add AND parent_id = @craft_parent AND position > @box_anchor;

INSERT INTO catalog_pages (parent_id, caption, icon, position, link, layout, catalog_mode, visible, enabled, required_club_level)
SELECT @craft_parent, 'Mystery Box', 90, @box_anchor + 1, 'xmas12_mysterybox', 'default_3x3', 'NORMAL', 1, 1, 0
FROM DUAL WHERE @box_add;

INSERT IGNORE INTO catalog_page_offers (page_id, offer_id, position)
SELECT c.id, @box_offer, 0 FROM catalog_pages c
WHERE c.link = 'xmas12_mysterybox' AND c.catalog_mode = 'NORMAL' AND @box_offer IS NOT NULL;

INSERT IGNORE INTO catalog_page_texts (page_id, slot, text)
SELECT c.id, 0, 'Complete your daily quests to get a key and activate your Mystery Box. Another player with a key of the same colour is needed to open it, and you both get a festive prize!'
FROM catalog_pages c WHERE c.link = 'xmas12_mysterybox' AND c.catalog_mode = 'NORMAL';

COMMIT;
