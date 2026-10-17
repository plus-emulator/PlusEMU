-- The Builders Club catalogue (AIR WIN63-202610151745-318700621): the client keeps a NORMAL and a BUILDERS_CLUB catalogue window
-- and asks for each index by mode (HabboCatalog.as:1744-1760). Pages get their mode back, and a link only has to be unique within
-- its mode, as on hhus where both catalogues have a 'furni' page.
-- user_builders_club holds a membership's end; a user without a row is on the trial. items_builders_club marks the furni a user
-- placed from the Builders Club catalogue; they count against the user's limit and are deleted when picked up.
-- The Builders Club tree starts as on hhus: Furni > Building Blocks, Alphabet Blocks, showing the same recolorable offers as the
-- normal pages of migration 87.
-- Idempotent: reruns end in the same state.

ALTER TABLE `catalog_pages`
    ADD COLUMN IF NOT EXISTS `catalog_mode` ENUM('NORMAL','BUILDERS_CLUB') NOT NULL DEFAULT 'NORMAL' AFTER `layout`;

ALTER TABLE `catalog_pages` DROP INDEX IF EXISTS `link`;
ALTER TABLE `catalog_pages` ADD UNIQUE KEY IF NOT EXISTS `mode_link` (`catalog_mode`, `link`);

CREATE TABLE IF NOT EXISTS `user_builders_club` (
    `user_id` INT(11) NOT NULL,
    `expires_at` DATETIME(6) NOT NULL,
    PRIMARY KEY (`user_id`),
    CONSTRAINT `fk_user_builders_club_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `items_builders_club` (
    `item_id` INT(10) UNSIGNED NOT NULL,
    `user_id` INT(11) NOT NULL,
    PRIMARY KEY (`item_id`),
    KEY `user_id` (`user_id`),
    CONSTRAINT `fk_items_builders_club_item_id` FOREIGN KEY (`item_id`) REFERENCES `items` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `catalog_pages` (`parent_id`, `caption`, `icon`, `position`, `link`, `layout`, `catalog_mode`, `visible`, `enabled`, `required_club_level`)
SELECT NULL, 'Furni', 2, 1, 'furni', 'default_3x3', 'BUILDERS_CLUB', 1, 1, 0
WHERE NOT EXISTS (SELECT 1 FROM `catalog_pages` WHERE `catalog_mode` = 'BUILDERS_CLUB' AND `link` = 'furni');

INSERT INTO `catalog_pages` (`parent_id`, `caption`, `icon`, `position`, `link`, `layout`, `catalog_mode`, `visible`, `enabled`, `required_club_level`)
SELECT `furni`.`id`, `page`.`caption`, `page`.`icon`, `page`.`position`, `page`.`link`, 'recolorable_furniture', 'BUILDERS_CLUB', 1, 1, 0
FROM `catalog_pages` AS `furni`
INNER JOIN (
    SELECT 'Building Blocks' AS `caption`, 28 AS `icon`, 1 AS `position`, 'bc_blocks_recolorable' AS `link`
    UNION ALL SELECT 'Alphabet Blocks', 101, 2, 'bc_alphabet_recolorable'
) AS `page`
WHERE `furni`.`catalog_mode` = 'BUILDERS_CLUB' AND `furni`.`link` = 'furni'
    AND NOT EXISTS (SELECT 1 FROM `catalog_pages` WHERE `catalog_mode` = 'BUILDERS_CLUB' AND `link` = `page`.`link`);

INSERT IGNORE INTO `catalog_page_offers` (`page_id`, `offer_id`, `position`)
SELECT `builders`.`id`, `offer`.`offer_id`, `offer`.`position`
FROM `catalog_pages` AS `builders`
INNER JOIN `catalog_pages` AS `normal` ON `normal`.`catalog_mode` = 'NORMAL' AND `normal`.`link` = `builders`.`link`
INNER JOIN `catalog_page_offers` AS `offer` ON `offer`.`page_id` = `normal`.`id`
WHERE `builders`.`catalog_mode` = 'BUILDERS_CLUB' AND `builders`.`link` IN ('bc_blocks_recolorable', 'bc_alphabet_recolorable');

INSERT IGNORE INTO `catalog_page_images` (`page_id`, `slot`, `image`)
SELECT `builders`.`id`, `image`.`slot`, `image`.`image`
FROM `catalog_pages` AS `builders`
INNER JOIN `catalog_pages` AS `normal` ON `normal`.`catalog_mode` = 'NORMAL' AND `normal`.`link` = `builders`.`link`
INNER JOIN `catalog_page_images` AS `image` ON `image`.`page_id` = `normal`.`id`
WHERE `builders`.`catalog_mode` = 'BUILDERS_CLUB' AND `builders`.`link` IN ('bc_blocks_recolorable', 'bc_alphabet_recolorable');

INSERT IGNORE INTO `catalog_page_texts` (`page_id`, `slot`, `text`)
SELECT `builders`.`id`, `text`.`slot`, `text`.`text`
FROM `catalog_pages` AS `builders`
INNER JOIN `catalog_pages` AS `normal` ON `normal`.`catalog_mode` = 'NORMAL' AND `normal`.`link` = `builders`.`link`
INNER JOIN `catalog_page_texts` AS `text` ON `text`.`page_id` = `normal`.`id`
WHERE `builders`.`catalog_mode` = 'BUILDERS_CLUB' AND `builders`.`link` IN ('bc_blocks_recolorable', 'bc_alphabet_recolorable');
