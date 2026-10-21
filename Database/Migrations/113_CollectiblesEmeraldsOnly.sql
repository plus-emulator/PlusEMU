-- The Collectibles hub without Habbo's blockchain parts: Emeralds are the only currency, there are no mint token packs, no Silver
-- transfer fee and no linked wallets. Linked wallet rows (is_collector = 0) stay in collectible_wallets, but the emulator no longer
-- reads them. Idempotent: a second run changes nothing.

ALTER TABLE collectible_shop_offers ADD COLUMN IF NOT EXISTS min_level INT(11) NOT NULL DEFAULT 0 AFTER emerald_price;

-- Only the first run still sees collectible_mint_token_offers. The steps that must happen once check it, so a rerun never locks
-- an offer again that staff set to 0 and never pays mint tokens twice.
SET @collectibles_first_run = EXISTS(SELECT 1 FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'collectible_mint_token_offers');

-- The level follows the price, so the cheapest offers only need Emeralds and the costly ones need a collector level too.
-- Staff edit the column freely afterwards.
SET @collectibles_shop_levels_sql = IF(@collectibles_first_run, 'UPDATE collectible_shop_offers SET min_level = CASE
    WHEN emerald_price <= 150 THEN 2
    WHEN emerald_price <= 300 THEN 3
    WHEN emerald_price <= 600 THEN 4
    WHEN emerald_price <= 1200 THEN 5
    ELSE 6
END
WHERE min_level = 0 AND emerald_price > 75', 'SELECT 1');
PREPARE collectibles_shop_levels FROM @collectibles_shop_levels_sql;
EXECUTE collectibles_shop_levels;
DEALLOCATE PREPARE collectibles_shop_levels;

-- A relic costs 50 Emeralds per Curator Stamp of Habbo's price, so a one-stamp relic stays in the price band of a common Shop offer.
-- emerald_price is filled while token_price still exists, so a run that stops halfway gives the same prices when it runs again.
ALTER TABLE collectible_mintable_items ADD COLUMN IF NOT EXISTS emerald_price INT(11) NOT NULL AFTER product_code;
SET @collectibles_relic_price_sql = IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'collectible_mintable_items' AND COLUMN_NAME = 'token_price'),
    'UPDATE collectible_mintable_items SET emerald_price = token_price * 50', 'SELECT 1');
PREPARE collectibles_relic_price FROM @collectibles_relic_price_sql;
EXECUTE collectibles_relic_price;
DEALLOCATE PREPARE collectibles_relic_price;
ALTER TABLE collectible_mintable_items DROP COLUMN IF EXISTS token_price;

-- Each mint token becomes 50 Emeralds, the price of one Curator Stamp in a relic. A FreshInstall.sql rebuilt after this file has no
-- mint_tokens column.
SET @collectibles_mint_tokens_sql = IF(@collectibles_first_run AND EXISTS(SELECT 1 FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'collectible_wallets' AND COLUMN_NAME = 'mint_tokens'),
    'INSERT INTO user_currencies (user_id, type, amount)
    SELECT user_id, 1001, SUM(mint_tokens) * 50 FROM collectible_wallets WHERE mint_tokens > 0 GROUP BY user_id
    ON DUPLICATE KEY UPDATE amount = amount + VALUES(amount)', 'SELECT 1');
PREPARE collectibles_mint_tokens FROM @collectibles_mint_tokens_sql;
EXECUTE collectibles_mint_tokens;
DEALLOCATE PREPARE collectibles_mint_tokens;

DROP TABLE IF EXISTS collectible_mint_token_offers;

ALTER TABLE collectible_wallets DROP COLUMN IF EXISTS mint_tokens;

DELETE FROM server_settings WHERE `key` = 'collectibles.transfer.fee';
