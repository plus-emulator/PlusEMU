-- The Collectibles hub without Habbo's blockchain parts: Emeralds are the only currency, there are no mint token packs, no Silver
-- transfer fee and no linked wallets. Linked wallet rows (is_collector = 0) stay in collectible_wallets, but the emulator no longer
-- reads them.
-- FreshInstall.sql keeps the hub tables in the shape 109 makes. A fresh install has no schema_migrations rows, so it runs 109, 111
-- and 113 at first start. 111 also fills hub tables that already have the shape this file makes.
-- Idempotent: a second run changes nothing.

DROP TABLE IF EXISTS collectible_mint_token_offers;

DELETE FROM server_settings WHERE `key` = 'collectibles.transfer.fee';
