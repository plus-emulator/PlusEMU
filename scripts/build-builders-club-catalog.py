#!/usr/bin/env python3
"""Generate the migration that mirrors Habbo's Builders Club catalogue tree.

Input: a read-only capture of hhus' BUILDERS_CLUB index and pages (GetCatalogIndex / GetCatalogPage with catalogType
BUILDERS_CLUB) and Habbo's furnidata_json of the same day. Every product's classname is resolved from Habbo's furnidata by its
sprite id and product type, never from ours; the migration then matches it to a `furniture` row by classname.

The migration it writes is idempotent:
- Builders Club pages keep Habbo's page ids, captions, icons, links, layouts, order, images and texts. Folders that Habbo sends
  without a page id (-1) get ids from 2100000000 up, in tree order, and stay disabled headings as on hhus.
- Builders Club pages that are not in the capture are removed.
- An offer reuses the catalog offer with Habbo's id when that offer sells the same furni once; otherwise one with the same
  localization key and furni; otherwise it is created, with Habbo's id when that id is free.
- Offers whose furni has no `furniture` row are left out and listed by the generator.

Usage: build-builders-club-catalog.py CAPTURE.json FURNIDATA.json OUT.sql [--furniture NAMES.txt]
"""
import argparse
import json
import sys

FOLDER_ID_BASE = 2100000000


def sql(value):
    if value is None:
        return 'NULL'
    if isinstance(value, bool):
        return '1' if value else '0'
    if isinstance(value, int):
        return str(value)
    return "'" + str(value).replace('\\', '\\\\').replace("'", "''").replace('\n', '\\n').replace('\r', '\\r') + "'"


def rows(values):
    return ',\n'.join('    (' + ', '.join(sql(v) for v in row) + ')' for row in values)


def chunks(values, size=500):
    for start in range(0, len(values), size):
        yield values[start:start + size]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('capture')
    parser.add_argument('furnidata')
    parser.add_argument('out')
    parser.add_argument('--furniture', help='known furniture item names, one per line, to report missing furni')
    args = parser.parse_args()

    capture = json.load(open(args.capture, encoding='utf-8'))
    furnidata = json.load(open(args.furnidata, encoding='utf-8'))
    by_type = {
        's': {entry['id']: entry for entry in furnidata['roomitemtypes']['furnitype']},
        'i': {entry['id']: entry for entry in furnidata['wallitemtypes']['furnitype']},
    }
    contents = {page['node']['PageId']: page['page'] for page in capture['bc_pages']}

    pages, images, texts, page_offers = [], [], [], []
    offers, unresolved = {}, []
    seen_page_offers = set()
    next_folder = [FOLDER_ID_BASE]

    def walk(node, parent_id, position):
        page_id = node['PageId']
        folder = page_id == -1

        if folder:
            next_folder[0] += 1
            page_id = next_folder[0]

        content = None if folder else contents[page_id]
        pages.append((len(pages), page_id, None if parent_id == -1 else parent_id, node['Localization'], node['Icon'], position, node['PageName'],
                      content['LayoutCode'] if content else 'default_3x3', node['Visible'], not folder))

        if content:
            for slot, image in enumerate(content['Localization']['Images']):
                images.append((page_id, slot, image))
            for slot, text in enumerate(content['Localization']['Texts']):
                texts.append((page_id, slot, text))
            for offer_position, offer in enumerate(content['Offers']):
                [product] = offer['Products']
                entry = by_type.get(product['ProductType'], {}).get(product['FurniClassId'])

                if entry is None or product['ProductCount'] != 1:
                    unresolved.append((page_id, offer['OfferId'], product['ProductType'], product['FurniClassId']))
                    continue

                # A few hhus offers carry -1 activity points (no price); prices are unsigned here.
                offers[offer['OfferId']] = (offer['OfferId'], offer['LocalizationId'], max(0, offer['PriceInCredits']), max(0, offer['PriceInActivityPoints']),
                                            offer['ActivityPointType'], offer['ClubLevel'], offer['BundlePurchaseAllowed'], offer['PreviewImage'],
                                            entry['classname'], product['ProductType'], product['ExtraParam'])
                # hhus lists a few offers twice on one page; the page shows each once, at its first place.
                if (page_id, offer['OfferId']) not in seen_page_offers:
                    seen_page_offers.add((page_id, offer['OfferId']))
                    page_offers.append((page_id, offer['OfferId'], offer_position))

        for child_position, child in enumerate(node.get('Children', [])):
            walk(child, page_id, child_position)

    for position, child in enumerate(capture['bc_index']['Root']['Children']):
        walk(child, -1, position)

    if unresolved:
        sys.exit(f'{len(unresolved)} products do not resolve in Habbo furnidata: {unresolved[:10]}')

    classnames = sorted({offer[8] for offer in offers.values()})
    missing = []

    if args.furniture:
        known = {line.strip().lower() for line in open(args.furniture, encoding='utf-8') if line.strip()}
        missing = [name for name in classnames if name.lower() not in known]

    out = []
    out.append(f"""-- Habbo's Builders Club catalogue, 1:1 (AIR {capture['build']}; {capture['hotel']} BUILDERS_CLUB index and pages captured
-- {capture['captured_utc']}, read-only). Generated by scripts/build-builders-club-catalog.py from that capture and Habbo's furnidata_json of the
-- same day; product classnames come from Habbo's furnidata by sprite id and match `furniture` rows by classname.
-- {len(pages)} pages ({next_folder[0] - FOLDER_ID_BASE} headings without a page), {len(offers)} offers, {len(page_offers)} page offers, {len(classnames)} furni.
-- Replaces the starter tree of migration 89. Idempotent: reruns end in the same state.

CREATE TEMPORARY TABLE `bc_pages_91` (
    `ord` INT NOT NULL, `id` INT NOT NULL PRIMARY KEY, `parent_id` INT NULL, `caption` VARCHAR(128) NOT NULL, `icon` INT NOT NULL, `position` INT NOT NULL,
    `link` VARCHAR(128) NOT NULL, `layout` VARCHAR(64) NOT NULL, `visible` TINYINT(1) NOT NULL, `enabled` TINYINT(1) NOT NULL
);
CREATE TEMPORARY TABLE `bc_page_images_91` (`page_id` INT NOT NULL, `slot` INT NOT NULL, `image` VARCHAR(255) NOT NULL, PRIMARY KEY (`page_id`, `slot`));
CREATE TEMPORARY TABLE `bc_page_texts_91` (`page_id` INT NOT NULL, `slot` INT NOT NULL, `text` TEXT NOT NULL, PRIMARY KEY (`page_id`, `slot`));
CREATE TEMPORARY TABLE `bc_offers_91` (
    `habbo_id` INT NOT NULL PRIMARY KEY, `localization_key` VARCHAR(100) NOT NULL, `cost_credits` INT UNSIGNED NOT NULL, `cost_points` INT UNSIGNED NOT NULL,
    `points_type` INT UNSIGNED NOT NULL, `club_level` TINYINT UNSIGNED NOT NULL, `bulk_purchase` TINYINT(1) NOT NULL, `preview_image` VARCHAR(255) NOT NULL,
    `classname` VARCHAR(100) NOT NULL, `product_type` CHAR(1) NOT NULL, `extra_param` VARCHAR(1024) NOT NULL,
    `furniture_id` INT UNSIGNED NULL, `offer_id` INT NULL, KEY (`localization_key`)
);
CREATE TEMPORARY TABLE `bc_page_offers_91` (`page_id` INT NOT NULL, `habbo_id` INT NOT NULL, `position` INT NOT NULL, PRIMARY KEY (`page_id`, `habbo_id`));
""")

    for table, columns, values in (
        ('bc_pages_91', '`ord`, `id`, `parent_id`, `caption`, `icon`, `position`, `link`, `layout`, `visible`, `enabled`', pages),
        ('bc_page_images_91', '`page_id`, `slot`, `image`', images),
        ('bc_page_texts_91', '`page_id`, `slot`, `text`', texts),
        ('bc_offers_91', '`habbo_id`, `localization_key`, `cost_credits`, `cost_points`, `points_type`, `club_level`, `bulk_purchase`, `preview_image`, '
                         '`classname`, `product_type`, `extra_param`', sorted(offers.values())),
        ('bc_page_offers_91', '`page_id`, `habbo_id`, `position`', page_offers),
    ):
        for part in chunks(values):
            out.append(f'INSERT INTO `{table}` ({columns}) VALUES\n{rows(part)};\n')

    out.append("""-- Each furni by classname: the row that carries its furnidata, else the first. furniture.item_name has no index, so the
-- pick per classname goes through a keyed table instead of one furniture scan per offer.
CREATE TEMPORARY TABLE `bc_furniture_91` (
    `item_name` VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL, `type` CHAR(1) NOT NULL, `id` INT UNSIGNED NOT NULL,
    PRIMARY KEY (`item_name`, `type`)
);
INSERT INTO `bc_furniture_91` (`item_name`, `type`, `id`)
SELECT `item_name`, `type`, `id` FROM (
    SELECT `item_name`, `type`, `id`, ROW_NUMBER() OVER (PARTITION BY `item_name`, `type` ORDER BY `has_furnidata` DESC, `id`) AS `pick`
    FROM `furniture`
) AS `candidate`
WHERE `pick` = 1;
UPDATE `bc_offers_91` AS `offer`
LEFT JOIN `bc_furniture_91` AS `furni` ON `furni`.`item_name` = `offer`.`classname` AND `furni`.`type` = `offer`.`product_type`
SET `offer`.`furniture_id` = `furni`.`id`;
DROP TEMPORARY TABLE `bc_furniture_91`;

-- Habbo's offer id when our offer of that id sells the same furni once.
UPDATE `bc_offers_91` AS `offer`
SET `offer`.`offer_id` = `offer`.`habbo_id`
WHERE `offer`.`furniture_id` IS NOT NULL
    AND (SELECT COUNT(*) FROM `catalog_offer_products` AS `product` WHERE `product`.`offer_id` = `offer`.`habbo_id`) = 1
    AND EXISTS (SELECT 1 FROM `catalog_offer_products` AS `product`
        WHERE `product`.`offer_id` = `offer`.`habbo_id` AND `product`.`product_type` = 'furni'
            AND `product`.`furniture_id` = `offer`.`furniture_id` AND `product`.`amount` = 1);

-- Otherwise an offer with the same localization key selling the same furni once (also what an earlier run created).
UPDATE `bc_offers_91` AS `offer`
SET `offer`.`offer_id` = (
    SELECT MIN(`existing`.`id`) FROM `catalog_offers` AS `existing`
    INNER JOIN `catalog_offer_products` AS `product` ON `product`.`offer_id` = `existing`.`id`
    WHERE `existing`.`localization_key` = `offer`.`localization_key` AND `product`.`product_type` = 'furni'
        AND `product`.`furniture_id` = `offer`.`furniture_id` AND `product`.`amount` = 1
        AND (SELECT COUNT(*) FROM `catalog_offer_products` AS `other` WHERE `other`.`offer_id` = `existing`.`id`) = 1)
WHERE `offer`.`offer_id` IS NULL AND `offer`.`furniture_id` IS NOT NULL;

-- Otherwise a new offer: with Habbo's id when it is free, else the next id.
INSERT INTO `catalog_offers` (`id`, `localization_key`, `cost_credits`, `cost_points`, `points_type`, `club_level`, `bulk_purchase`, `enabled`, `preview_image`)
SELECT IF(EXISTS (SELECT 1 FROM `catalog_offers` AS `taken` WHERE `taken`.`id` = `offer`.`habbo_id`), NULL, `offer`.`habbo_id`),
    `offer`.`localization_key`, `offer`.`cost_credits`, `offer`.`cost_points`, `offer`.`points_type`, `offer`.`club_level`, `offer`.`bulk_purchase`, 1,
    `offer`.`preview_image`
FROM `bc_offers_91` AS `offer`
WHERE `offer`.`offer_id` IS NULL AND `offer`.`furniture_id` IS NOT NULL
ORDER BY `offer`.`habbo_id`;

-- The new offers, the only ones of their key without a product, get their furni.
INSERT IGNORE INTO `catalog_offer_products` (`offer_id`, `position`, `product_type`, `furniture_id`, `amount`, `extra_param`)
SELECT `created`.`id`, 0, 'furni', `offer`.`furniture_id`, 1, `offer`.`extra_param`
FROM `bc_offers_91` AS `offer`
INNER JOIN `catalog_offers` AS `created` ON `created`.`localization_key` = `offer`.`localization_key`
WHERE `offer`.`offer_id` IS NULL AND `offer`.`furniture_id` IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM `catalog_offer_products` AS `product` WHERE `product`.`offer_id` = `created`.`id`);

UPDATE `bc_offers_91` AS `offer`
SET `offer`.`offer_id` = (
    SELECT MIN(`existing`.`id`) FROM `catalog_offers` AS `existing`
    INNER JOIN `catalog_offer_products` AS `product` ON `product`.`offer_id` = `existing`.`id`
    WHERE `existing`.`localization_key` = `offer`.`localization_key` AND `product`.`product_type` = 'furni'
        AND `product`.`furniture_id` = `offer`.`furniture_id` AND `product`.`amount` = 1)
WHERE `offer`.`offer_id` IS NULL AND `offer`.`furniture_id` IS NOT NULL;

-- Builders Club pages not in Habbo's tree (the starter tree of migration 89) go.
UPDATE `catalog_pages` SET `parent_id` = NULL
WHERE `catalog_mode` = 'BUILDERS_CLUB' AND `id` NOT IN (SELECT `id` FROM `bc_pages_91`);
DELETE FROM `catalog_pages`
WHERE `catalog_mode` = 'BUILDERS_CLUB' AND `id` NOT IN (SELECT `id` FROM `bc_pages_91`);

-- Parents come first in bc_pages_91, as in the tree.
INSERT INTO `catalog_pages` (`id`, `parent_id`, `caption`, `icon`, `position`, `link`, `layout`, `catalog_mode`, `visible`, `enabled`, `required_club_level`)
SELECT `id`, `parent_id`, `caption`, `icon`, `position`, `link`, `layout`, 'BUILDERS_CLUB', `visible`, `enabled`, 0
FROM `bc_pages_91`
ORDER BY `ord`
ON DUPLICATE KEY UPDATE `parent_id` = VALUES(`parent_id`), `caption` = VALUES(`caption`), `icon` = VALUES(`icon`), `position` = VALUES(`position`),
    `link` = VALUES(`link`), `layout` = VALUES(`layout`), `catalog_mode` = 'BUILDERS_CLUB', `visible` = VALUES(`visible`), `enabled` = VALUES(`enabled`);

DELETE FROM `catalog_page_images` WHERE `page_id` IN (SELECT `id` FROM `bc_pages_91`);
INSERT INTO `catalog_page_images` (`page_id`, `slot`, `image`) SELECT `page_id`, `slot`, `image` FROM `bc_page_images_91`;
DELETE FROM `catalog_page_texts` WHERE `page_id` IN (SELECT `id` FROM `bc_pages_91`);
INSERT INTO `catalog_page_texts` (`page_id`, `slot`, `text`) SELECT `page_id`, `slot`, `text` FROM `bc_page_texts_91`;

DELETE FROM `catalog_page_offers` WHERE `page_id` IN (SELECT `id` FROM `bc_pages_91`);
INSERT IGNORE INTO `catalog_page_offers` (`page_id`, `offer_id`, `position`)
SELECT `page`.`page_id`, `offer`.`offer_id`, `page`.`position`
FROM `bc_page_offers_91` AS `page`
INNER JOIN `bc_offers_91` AS `offer` ON `offer`.`habbo_id` = `page`.`habbo_id`
WHERE `offer`.`offer_id` IS NOT NULL;

DROP TEMPORARY TABLE `bc_pages_91`;
DROP TEMPORARY TABLE `bc_page_images_91`;
DROP TEMPORARY TABLE `bc_page_texts_91`;
DROP TEMPORARY TABLE `bc_offers_91`;
DROP TEMPORARY TABLE `bc_page_offers_91`;
""")

    with open(args.out, 'w', encoding='utf-8', newline='\n') as handle:
        handle.write('\n'.join(out))

    print(json.dumps({'pages': len(pages), 'headings': next_folder[0] - FOLDER_ID_BASE, 'offers': len(offers), 'page_offers': len(page_offers),
                      'classnames': len(classnames), 'missing_furniture': missing}))


if __name__ == '__main__':
    main()
