-- The October wired boxes of the r88 dossier, the WIRED Label and the WIRED Link (official furnidata WIN63-202610131318, sprites
-- 19393-19409 and the Web API add-on 19190). Adds their furniture rows where furnidata has none yet, gives every row the interaction
-- the engine needs, sells them on the Wired pages, and creates the tables for label bubbles, link networks and Web API keys.
-- Box rows keep the generic category interaction (ItemDefinition.WiredDescriptor reads the canonical item name); the label is
-- 'wired_label' and the link 'wired_link'. Idempotent: reruns change nothing.

-- 1. Tables (DDL commits implicitly, so it runs before the transaction). Rows go with their furni (items ON DELETE CASCADE); networks belong to a player.
CREATE TABLE IF NOT EXISTS `wired_labels` (
    `item_id` INT(10) UNSIGNED NOT NULL,
    `text` TEXT NOT NULL,
    `bubble_color` INT(11) NOT NULL DEFAULT 4278912,
    `visibility` TINYINT(1) NOT NULL DEFAULT 1,
    `always_open` TINYINT(1) NOT NULL DEFAULT 0,
    PRIMARY KEY (`item_id`),
    CONSTRAINT `fk_wired_labels_item_id` FOREIGN KEY (`item_id`) REFERENCES `items` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `wired_link_networks` (
    `id` INT(10) UNSIGNED NOT NULL AUTO_INCREMENT,
    `owner_id` INT(11) NOT NULL,
    `name` VARCHAR(255) NOT NULL,
    PRIMARY KEY (`id`),
    KEY `wired_link_networks_owner` (`owner_id`),
    CONSTRAINT `fk_wired_link_networks_owner_id` FOREIGN KEY (`owner_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `wired_link_endpoints` (
    `item_id` INT(10) UNSIGNED NOT NULL,
    `network_id` INT(10) UNSIGNED NOT NULL,
    `receive_local` TINYINT(1) NOT NULL DEFAULT 0,
    PRIMARY KEY (`item_id`),
    KEY `wired_link_endpoints_network` (`network_id`),
    CONSTRAINT `fk_wired_link_endpoints_item_id` FOREIGN KEY (`item_id`) REFERENCES `items` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_wired_link_endpoints_network_id` FOREIGN KEY (`network_id`) REFERENCES `wired_link_networks` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Only SHA-256 hashes of the keys; the add-on's own saved text holds the keys themselves.
CREATE TABLE IF NOT EXISTS `wired_web_api_keys` (
    `item_id` INT(10) UNSIGNED NOT NULL,
    `room_id` INT(10) UNSIGNED NOT NULL,
    `read_key_hash` CHAR(64) NULL DEFAULT NULL,
    `write_key_hash` CHAR(64) NULL DEFAULT NULL,
    `bulk_delete` TINYINT(1) NOT NULL DEFAULT 0,
    PRIMARY KEY (`item_id`),
    UNIQUE KEY `wired_web_api_read_key` (`read_key_hash`),
    UNIQUE KEY `wired_web_api_write_key` (`write_key_hash`),
    CONSTRAINT `fk_wired_web_api_keys_item_id` FOREIGN KEY (`item_id`) REFERENCES `items` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

START TRANSACTION;

-- 2. Furnidata rows (classname, sprite, name, description, revision, customparams, stand/sit/lay/put on, height, interaction).
-- Three classnames only have placeholder names in official furnidata; they get readable ones, as migration 79 did.
CREATE TEMPORARY TABLE wired_furni_81 (
    item_name VARCHAR(70) NOT NULL PRIMARY KEY,
    sprite_id INT NOT NULL,
    name VARCHAR(56) NOT NULL,
    description VARCHAR(255) NOT NULL,
    revision INT NOT NULL,
    custom_params VARCHAR(16) NULL,
    can_stand_on TINYINT(1) NOT NULL,
    can_sit_on TINYINT(1) NOT NULL,
    can_lay_on TINYINT(1) NOT NULL,
    can_put_stuff_on TINYINT(1) NOT NULL,
    height DOUBLE NOT NULL,
    interaction VARCHAR(25) NOT NULL
) ENGINE=MEMORY;
INSERT INTO wired_furni_81 VALUES
    ('wf_act_progress_ach', 19393, 'WIRED Effect: Progress Achievement', 'wf_act_progress_ach desc', 75953, '51', 1, 0, 0, 1, 0.65, 'wired_effect'),
    ('wf_act_send_dataframe', 19394, 'WIRED Effect: Send Link Dataframe', 'wf_act_send_dataframe desc', 75953, '64', 1, 0, 0, 1, 0.65, 'wired_effect'),
    ('wf_act_send_message', 19395, 'WIRED Effect: Send Link Message', 'wf_act_send_message desc', 75953, '63', 1, 0, 0, 1, 0.65, 'wired_effect'),
    ('wf_act_set_highlight', 19396, 'WIRED Effect: Set Furni Highlight', 'wf_act_set_highlight desc', 75953, '62', 1, 0, 0, 1, 0.65, 'wired_effect'),
    ('wf_act_set_visibility', 19397, 'WIRED Effect: Set Furni Transparency', 'wf_act_set_visibility desc', 75953, '61', 1, 0, 0, 1, 0.65, 'wired_effect'),
    ('wf_label', 19398, 'WIRED Label', 'wf_label desc', 75953, NULL, 0, 0, 0, 1, 0.11, 'wired_label'),
    ('wf_link', 19399, 'WIRED Link', 'wf_link desc', 75953, NULL, 0, 0, 0, 0, 1.8, 'wired_link'),
    ('wf_trg_receive_dataframe', 19400, 'WIRED Trigger: Receive Link Dataframe', 'wf_trg_receive_dataframe desc', 75953, '30', 1, 0, 0, 1, 0.65, 'wired_trigger'),
    ('wf_trg_receive_message', 19401, 'WIRED Trigger: Receive Link Message', 'wf_trg_receive_message desc', 75953, '29', 1, 0, 0, 1, 0.65, 'wired_trigger'),
    ('wf_trg_room_initializes', 19402, 'WIRED Trigger: Room Initialized', 'wf_trg_room_initializes desc', 75953, '28', 1, 0, 0, 1, 0.65, 'wired_trigger'),
    ('wf_var_dailytask', 19403, 'WIRED Variable: Daily Task', 'wf_var_dailytask desc', 75953, '8', 1, 0, 0, 1, 1.2, 'wired_variable'),
    ('wf_var_pointer', 19404, 'WIRED Variable: Pointer Variable', 'wf_var_pointer desc', 75953, '9', 1, 0, 0, 1, 1.2, 'wired_variable'),
    ('wf_xtra_achievement_enabler', 19405, 'WIRED Add-on: Achievement Enabler', 'wf_xtra_achievement_enabler desc', 75953, '2001', 1, 0, 0, 1, 0.74, 'wired_addon'),
    ('wf_xtra_link_speaker', 19406, 'WIRED Link Speaker', 'wf_xtra_link_speaker desc', 75953, '2003', 0, 0, 0, 0, 1.0, 'wired_addon'),
    ('wf_xtra_var_import', 19407, 'Variable Add-on: Import', 'wf_xtra_var_import desc', 75953, '1004', 1, 0, 0, 1, 0.75, 'wired_addon'),
    ('wf_xtra_var_leaderboard', 19408, 'Variable Add-on: Leaderboard', 'wf_xtra_var_leaderboard desc', 75953, '1005', 1, 0, 0, 1, 0.75, 'wired_addon'),
    ('wf_xtra_var_share', 19409, 'Variable Add-on: Share', 'wf_xtra_var_share desc', 75953, '1003', 1, 0, 0, 1, 0.75, 'wired_addon'),
    ('wf_xtra_var_web_api', 19190, 'Variable Global Add-on: Web API', 'wf_xtra_var_web_api desc', 75694, '2002', 1, 0, 0, 1, 0.75, 'wired_addon');

-- A classname or sprite already taken by a furnidata row (60_FurnitureFurnidata) keeps that row.
INSERT INTO furniture (item_name, public_name, type, width, length, stack_height, can_stack, can_sit, is_walkable, sprite_id,
    allow_recycle, allow_trade, allow_marketplace_sell, allow_gift, allow_inventory_stack, interaction_type, interaction_modes_count,
    has_furnidata, revision, category, default_dir, xdim, ydim, name, description, custom_params, special_type,
    can_stand_on, can_sit_on, can_lay_on, can_put_stuff_on, height, furni_line, rare, tradeable, recyclable)
SELECT w.item_name, w.name, 's', 1, 1, w.height, w.can_put_stuff_on, 0, w.can_stand_on, w.sprite_id,
    1, 1, 1, 1, 1, w.interaction, 1,
    1, w.revision, 'wired', 0, 1, 1, w.name, w.description, w.custom_params, 1,
    w.can_stand_on, w.can_sit_on, w.can_lay_on, w.can_put_stuff_on, w.height, 'wired', 0, 1, 1
FROM wired_furni_81 w
WHERE NOT EXISTS (SELECT 1 FROM furniture f WHERE f.type = 's' AND f.furnidata_classname = w.item_name)
  AND NOT EXISTS (SELECT 1 FROM furniture f WHERE f.type = 's' AND f.furnidata_sprite_id = w.sprite_id);

-- Existing rows (the Web API add-on shipped as 'default') take the interaction the engine needs.
UPDATE furniture f JOIN wired_furni_81 w ON w.item_name = f.item_name AND w.sprite_id = f.sprite_id
SET f.interaction_type = w.interaction
WHERE f.type = 's' AND f.interaction_type NOT IN (w.interaction, w.item_name);

-- 3. Catalogue offers on the Wired pages, priced like their page; already sold on the page means nothing to add.
CREATE TEMPORARY TABLE wired_offer_plan_81 (
    item_name VARCHAR(70) NOT NULL PRIMARY KEY,
    sprite_id INT NOT NULL,
    page_link VARCHAR(128) NOT NULL,
    cost_credits INT UNSIGNED NOT NULL,
    bulk_purchase TINYINT(1) NOT NULL,
    sort_order INT NOT NULL,
    furniture_id INT UNSIGNED NULL,
    page_id INT NULL,
    offer_id INT NULL
) ENGINE=MEMORY;
INSERT INTO wired_offer_plan_81 (item_name, sprite_id, page_link, cost_credits, bulk_purchase, sort_order) VALUES
    ('wf_trg_room_initializes', 19402, 'wired_triggers', 6, 1, 1),
    ('wf_trg_receive_message', 19401, 'wired_triggers', 6, 1, 2),
    ('wf_trg_receive_dataframe', 19400, 'wired_triggers', 6, 1, 3),
    ('wf_act_progress_ach', 19393, 'wired_effects', 7, 1, 1),
    ('wf_act_set_visibility', 19397, 'wired_effects', 7, 1, 2),
    ('wf_act_set_highlight', 19396, 'wired_effects', 7, 1, 3),
    ('wf_act_send_message', 19395, 'wired_effects', 7, 1, 4),
    ('wf_act_send_dataframe', 19394, 'wired_effects', 7, 1, 5),
    ('wf_xtra_achievement_enabler', 19405, 'wired_addons', 5, 1, 1),
    ('wf_xtra_link_speaker', 19406, 'wired_addons', 5, 1, 2),
    ('wf_var_dailytask', 19403, 'wired_variables', 7, 1, 1),
    ('wf_var_pointer', 19404, 'wired_variables', 7, 1, 2),
    ('wf_xtra_var_share', 19409, 'wired_variables_addons', 8, 1, 1),
    ('wf_xtra_var_import', 19407, 'wired_variables_addons', 8, 1, 2),
    ('wf_xtra_var_leaderboard', 19408, 'wired_variables_addons', 8, 1, 3),
    ('wf_xtra_var_web_api', 19190, 'wired_variables_addons', 8, 1, 4),
    ('wf_label', 19398, 'wired_extras', 5, 1, 1),
    ('wf_link', 19399, 'wired_extras', 5, 1, 2);

UPDATE wired_offer_plan_81 p
JOIN furniture f ON f.furnidata_classname = p.item_name AND f.type = 's' AND f.sprite_id = p.sprite_id
SET p.furniture_id = f.id;
UPDATE wired_offer_plan_81 p JOIN catalog_pages c ON c.link = p.page_link SET p.page_id = c.id;

DELETE p FROM wired_offer_plan_81 p
WHERE p.furniture_id IS NULL OR p.page_id IS NULL OR EXISTS (
    SELECT 1 FROM catalog_page_offers po
    JOIN catalog_offer_products op ON op.offer_id = po.offer_id AND op.product_type = 'furni'
    WHERE po.page_id = p.page_id AND op.furniture_id = p.furniture_id);

SET @wired_offer_floor_81 = (SELECT COALESCE(MAX(id), 0) FROM catalog_offers);
INSERT INTO catalog_offers (localization_key, cost_credits, cost_points, points_type, club_level, bulk_purchase, enabled, preview_image)
SELECT p.item_name, p.cost_credits, 0, 0, 0, p.bulk_purchase, 1, ''
FROM wired_offer_plan_81 p ORDER BY p.page_link, p.sort_order;

UPDATE wired_offer_plan_81 p
JOIN catalog_offers o ON o.id > @wired_offer_floor_81 AND o.localization_key = p.item_name
SET p.offer_id = o.id;

INSERT INTO catalog_offer_products (offer_id, position, product_type, furniture_id, amount, extra_param)
SELECT p.offer_id, 0, 'furni', p.furniture_id, 1, '' FROM wired_offer_plan_81 p WHERE p.offer_id IS NOT NULL;

INSERT INTO catalog_page_offers (page_id, offer_id, position)
SELECT p.page_id, p.offer_id,
       (SELECT COALESCE(MAX(po.position), -1) FROM catalog_page_offers po WHERE po.page_id = p.page_id) + p.sort_order
FROM wired_offer_plan_81 p WHERE p.offer_id IS NOT NULL;

DROP TEMPORARY TABLE wired_offer_plan_81;
DROP TEMPORARY TABLE wired_furni_81;

COMMIT;
