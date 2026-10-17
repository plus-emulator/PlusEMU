-- Builders Club membership sold in the catalogue like Habbo Club (AIR WIN63-202610151745-318700621). hhus lists three Builders Club pages
-- under Memberships in the NORMAL catalogue: 'Builders Club' (builders_club_frontpage), 'Builders Club (Diamonds)' (builders_club_loyalty)
-- and 'BC Upgrades' (builders_club_addons). The loyalty and addons layouts list the page's offers with a buy button that opens the
-- ordinary purchase confirmation (BuilderLoyaltyCatalogWidget.as:20-58, BuilderAddonsCatalogWidget.as:22-70); the offers carry no
-- product, so the server knows them by what they sell. Offer ids and names are hhus' where hhus has them (3 months has no hhus
-- offer and gets a custom id); product names come from
-- Habbo's productdata.
-- - builders_club: days of membership; extra_param is the furni limit the package gives (productdata: 31 days 1000, 3 months 1500).
-- - builders_club_addon: furni added to a running membership's limit.
-- Credit prices follow the Habbo Club offers (31 days 100, 93 days 250); hhus' diamond and addon prices are kept.
-- Idempotent: reruns end in the same state.

ALTER TABLE `catalog_offer_products`
    MODIFY COLUMN `product_type` ENUM('furni','effect','badge','bot','pet','habbicon','builders_club','builders_club_addon') NOT NULL;

ALTER TABLE `user_builders_club`
    ADD COLUMN IF NOT EXISTS `furni_limit` INT(10) UNSIGNED NOT NULL DEFAULT 0 AFTER `expires_at`,
    ADD COLUMN IF NOT EXISTS `furni_addon` INT(10) UNSIGNED NOT NULL DEFAULT 0 AFTER `furni_limit`;

CREATE TEMPORARY TABLE `builders_club_offers_90` (
    `id` INT NOT NULL PRIMARY KEY,
    `localization_key` VARCHAR(100) NOT NULL,
    `cost_credits` INT UNSIGNED NOT NULL,
    `cost_points` INT UNSIGNED NOT NULL,
    `points_type` INT UNSIGNED NOT NULL,
    `product_type` VARCHAR(32) NOT NULL,
    `amount` INT UNSIGNED NOT NULL,
    `extra_param` VARCHAR(16) NOT NULL,
    `page_link` VARCHAR(128) NOT NULL,
    `position` INT NOT NULL
) ENGINE=MEMORY;
INSERT INTO `builders_club_offers_90` VALUES
    (13275, 'builders_club_31_days', 100, 0, 0, 'builders_club', 31, '1000', 'builders_club_membership', 0),
    (1890000001, 'builders_club_3_months', 250, 0, 0, 'builders_club', 93, '1500', 'builders_club_membership', 1),
    (13902, 'loyalty_bc_31_days', 0, 105, 5, 'builders_club', 31, '1000', 'loyalty_bc', 0),
    (13212, 'builders_club_addon_45furnis', 10, 0, 0, 'builders_club_addon', 45, '', 'builders_upgrades', 0),
    (13199, 'builders_club_addon_250furnis', 50, 0, 0, 'builders_club_addon', 250, '', 'builders_upgrades', 1),
    (13211, 'builders_club_addon_600furnis', 100, 0, 0, 'builders_club_addon', 600, '', 'builders_upgrades', 2);

INSERT INTO `catalog_offers` (`id`, `localization_key`, `cost_credits`, `cost_points`, `points_type`, `club_level`, `bulk_purchase`, `enabled`, `preview_image`)
SELECT `id`, `localization_key`, `cost_credits`, `cost_points`, `points_type`, 0, 0, 1, ''
FROM `builders_club_offers_90`
ON DUPLICATE KEY UPDATE `localization_key` = VALUES(`localization_key`), `cost_credits` = VALUES(`cost_credits`), `cost_points` = VALUES(`cost_points`),
    `points_type` = VALUES(`points_type`), `bulk_purchase` = 0;

DELETE `product` FROM `catalog_offer_products` AS `product`
INNER JOIN `builders_club_offers_90` AS `offer` ON `offer`.`id` = `product`.`offer_id`;

INSERT INTO `catalog_offer_products` (`offer_id`, `position`, `product_type`, `amount`, `extra_param`)
SELECT `id`, 0, `product_type`, `amount`, `extra_param` FROM `builders_club_offers_90`;

INSERT INTO `catalog_pages` (`parent_id`, `caption`, `icon`, `position`, `link`, `layout`, `catalog_mode`, `visible`, `enabled`, `required_club_level`)
SELECT `memberships`.`id`, `page`.`caption`, `page`.`icon`, `page`.`position`, `page`.`link`, `page`.`layout`, 'NORMAL', 1, 1, 0
FROM `catalog_pages` AS `memberships`
INNER JOIN (
    SELECT 'Builders Club' AS `caption`, 193 AS `icon`, 2 AS `position`, 'builders_club_frontpage' AS `link`, 'builders_club_frontpage' AS `layout`
    UNION ALL SELECT 'Builders Club Membership', 193, 3, 'builders_club_membership', 'builders_club_loyalty'
    UNION ALL SELECT 'Builders Club (Diamonds)', 193, 4, 'loyalty_bc', 'builders_club_loyalty'
    UNION ALL SELECT 'BC Upgrades', 194, 5, 'builders_upgrades', 'builders_club_addons'
) AS `page`
WHERE `memberships`.`catalog_mode` = 'NORMAL' AND `memberships`.`link` = 'habbo_club_desktop'
    AND NOT EXISTS (SELECT 1 FROM `catalog_pages` WHERE `catalog_mode` = 'NORMAL' AND `link` = `page`.`link`);

INSERT INTO `catalog_page_texts` (`page_id`, `slot`, `text`)
SELECT `page`.`id`, 0, `text`.`text`
FROM `catalog_pages` AS `page`
INNER JOIN (
    SELECT 'builders_club_frontpage' AS `link`, '<b><font color="#cc6600" size="16">What is Builders Club?</font></b><br><br>Have you ever wanted to build a room with huge collection of furni? Tired of counting your credits to finish a room? Or do you simply want to let your creativity flow? Builders Club is your ultimate solution!<br><br><b><font color="#cc6600" size="16">It''s as easy as 1-2- Free</font></b><br><img src="https://images.habbo.com/c_images/catalogue/bc_instr_1.png"/><br><br><br><br><br><br><br><br><ul><li>Select one of <a href="event:navigator/tab/me">your rooms</a> (or make a new one).</li><li>Open the Builders Club Warehouse</li><li>Drag and drop items into your room</li></ul><br><br><i>Hint: If you want to pick up all the builders club furni from a room, type ":pickallbc" into the chat and hit enter.</i><br><br>Click here to learn more about <a href="event:habbopages/builders-club/info">builders club member benefits.</a> If you have specific questions, you can also check out our <a href="event:habbopages/builders-club/faq">FAQs</a> page!' AS `text`
    UNION ALL SELECT 'builders_club_membership', '<b><font color="#cc6600" size="16">What is Builders Club?</font></b><br><br>Have you always dreamed of building a room with tons of furni? Builders Club is your ultimate solution! Click <a href="event:habbopages/builders-club/info">here</a> to learn more.'
    UNION ALL SELECT 'loyalty_bc', '<b><font color="#cc6600" size="16">What is Builders Club?</font></b><br><br>Have you always dreamed of building a room with tons of furni? Builders Club is your ultimate solution! Click <a href="event:habbopages/builders-club/info">here</a> to learn more.'
    UNION ALL SELECT 'builders_upgrades', '<b><font color="#d85b00" size="16">Builders Club Upgrades</font></b><br><br><font align="justify">Have you reached your <b> Furni Limit </b> but want to <b> keep building? </b> No problem! Simply increase your furni limits and creative possibilities!'
) AS `text` ON `text`.`link` = `page`.`link`
WHERE `page`.`catalog_mode` = 'NORMAL'
ON DUPLICATE KEY UPDATE `text` = VALUES(`text`);

INSERT INTO `catalog_page_offers` (`page_id`, `offer_id`, `position`)
SELECT `page`.`id`, `offer`.`id`, `offer`.`position`
FROM `builders_club_offers_90` AS `offer`
INNER JOIN `catalog_pages` AS `page` ON `page`.`catalog_mode` = 'NORMAL' AND `page`.`link` = `offer`.`page_link`
ON DUPLICATE KEY UPDATE `position` = VALUES(`position`);

DROP TEMPORARY TABLE `builders_club_offers_90`;
