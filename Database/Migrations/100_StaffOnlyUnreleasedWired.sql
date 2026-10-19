-- Official Habbo sells none of these Wired boxes: AIR WIN63-202610151745-318700621 furnidata has no catalogue offer and no Builders
-- Club offer for them, and the public catalogue does not list them. Migration 82 kept them buyable on a local 'More Wired' page, so any
-- player could buy them, including Progress Achievement with Achievement Enabler, which hands out any achievement.
-- Every offer selling one of them moves from a public page to a staff-only 'Wired (staff)' page under Staff (or at the top when the
-- hotel has no Staff page). Catalogue search only finds offers on pages without a required permission, so players no longer find
-- them there either. 'More Wired' is switched off once it is empty. Idempotent: reruns end in the same state.

START TRANSACTION;

CREATE TEMPORARY TABLE staff_only_wired_100 (item_name VARCHAR(100) NOT NULL PRIMARY KEY) ENGINE=MEMORY;
INSERT INTO staff_only_wired_100 VALUES ('wf_act_move_furni_to'),('wf_act_mute_triggerer'),('wf_act_progress_ach'),('wf_act_send_dataframe'),('wf_act_send_message'),('wf_act_set_highlight'),('wf_act_set_visibility'),('wf_cnd_not_wearing_b'),('wf_cnd_not_wearing_fx'),('wf_cnd_wearing_badge'),('wf_cnd_wearing_effect'),('wf_link'),('wf_storage_furni_starter'),('wf_trg_at_time_long'),('wf_trg_receive_dataframe'),('wf_trg_receive_message'),('wf_trg_room_initializes'),('wf_var_dailytask'),('wf_var_pointer'),('wf_var_quest'),('wf_var_quest_chain'),('wf_xtra_achievement_enabler'),('wf_xtra_link_speaker'),('wf_xtra_var_import'),('wf_xtra_var_share');

INSERT INTO catalog_pages (parent_id, caption, icon, required_permission, position, link, layout, catalog_mode, visible, enabled, required_club_level)
SELECT s.id, 'Wired (staff)', 80, 'catalog.pages.administrator',
    (SELECT COALESCE(MAX(c.position), -1) + 1 FROM catalog_pages c WHERE c.parent_id = s.id), 'staff_wired', 'default_3x3', 'NORMAL', 1, 1, 0
FROM catalog_pages s
WHERE s.link = 'staff' AND NOT EXISTS (SELECT 1 FROM catalog_pages c WHERE c.link = 'staff_wired')
ORDER BY s.id LIMIT 1;

INSERT INTO catalog_pages (parent_id, caption, icon, required_permission, position, link, layout, catalog_mode, visible, enabled, required_club_level)
SELECT NULL, 'Wired (staff)', 80, 'catalog.pages.administrator',
    (SELECT COALESCE(MAX(c.position), -1) + 1 FROM catalog_pages c WHERE c.parent_id IS NULL), 'staff_wired', 'default_3x3', 'NORMAL', 1, 1, 0
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM catalog_pages c WHERE c.link = 'staff_wired');

UPDATE catalog_pages SET required_permission = 'catalog.pages.administrator' WHERE link = 'staff_wired';

INSERT IGNORE INTO catalog_page_images (page_id, slot, image)
SELECT c.id, s.slot, IF(s.slot = 0, 'catalog_wired_header1', '') FROM catalog_pages c
JOIN (SELECT 0 AS slot UNION ALL SELECT 1 UNION ALL SELECT 2) s
WHERE c.link = 'staff_wired';
INSERT IGNORE INTO catalog_page_texts (page_id, slot, text)
SELECT c.id, s.slot, '' FROM catalog_pages c
JOIN (SELECT 0 AS slot UNION ALL SELECT 1 UNION ALL SELECT 2) s
WHERE c.link = 'staff_wired';

-- Pages a player can open: no required permission on the page or on any page above it.
CREATE TEMPORARY TABLE public_pages_100 (id INT NOT NULL PRIMARY KEY) ENGINE=MEMORY;
INSERT INTO public_pages_100 (id)
WITH RECURSIVE gated (id) AS (
    SELECT id FROM catalog_pages WHERE required_permission IS NOT NULL AND required_permission <> ''
    UNION
    SELECT c.id FROM catalog_pages c JOIN gated g ON c.parent_id = g.id
)
SELECT p.id FROM catalog_pages p WHERE p.id NOT IN (SELECT id FROM gated);

CREATE TEMPORARY TABLE staff_only_offers_100 (offer_id INT NOT NULL PRIMARY KEY) ENGINE=MEMORY;
INSERT IGNORE INTO staff_only_offers_100 (offer_id)
SELECT po.offer_id FROM catalog_page_offers po
JOIN public_pages_100 pp ON pp.id = po.page_id
JOIN catalog_offer_products cop ON cop.offer_id = po.offer_id
JOIN furniture f ON f.id = cop.furniture_id
JOIN staff_only_wired_100 w ON w.item_name = f.item_name;

-- A temporary table can only be opened once per statement (MySQL), so the order comes from ROW_NUMBER, not a second read.
INSERT IGNORE INTO catalog_page_offers (page_id, offer_id, position)
SELECT c.id, o.offer_id,
    (SELECT COALESCE(MAX(x.position), -1) + 1 FROM catalog_page_offers x WHERE x.page_id = c.id) + ROW_NUMBER() OVER (ORDER BY o.offer_id) - 1
FROM staff_only_offers_100 o
JOIN catalog_pages c ON c.link = 'staff_wired';

DELETE po FROM catalog_page_offers po
JOIN public_pages_100 pp ON pp.id = po.page_id
JOIN staff_only_offers_100 o ON o.offer_id = po.offer_id;

UPDATE catalog_pages p SET p.enabled = 0
WHERE p.link = 'plus_wired_more' AND NOT EXISTS (SELECT 1 FROM catalog_page_offers po WHERE po.page_id = p.id);

DROP TEMPORARY TABLE staff_only_offers_100;
DROP TEMPORARY TABLE public_pages_100;
DROP TEMPORARY TABLE staff_only_wired_100;

COMMIT;
