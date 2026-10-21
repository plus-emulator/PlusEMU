-- Collectibles hub (AIR com/sulake/habbo/catalog/collectibles). Habbo keeps collectibles on a blockchain; this hotel keeps them here.
-- collectible_collections: the sets in the Collections tab, with the boost score a completed set adds and its bonus and reward products.
-- collectible_products: every product a collectible can be. collection_id puts a product in a set; bonus and reward products have none.
-- collectible_wallets: each user's Collector Wallet (is_collector = 1, made on first use) and the linked wallets staff add.
-- user_collectibles: the collectibles a wallet owns. serial_number counts 1, 2, 3 ... per product. They never put furni in the inventory.
-- collectible_shop_offers: the Shop tab, priced in emeralds. mint_limit -1 means no limit.
-- collectible_mint_token_offers: mint token packs bought with silver.
-- collectible_mintable_items: furni that can be minted (item_type s = floor, i = wall) and the product it becomes.
--   A row needs ends_at for the client to allow minting, because AIR treats an end time of 0 as ended (MintInventoryListTab.as isMintPeriodExpired).
-- collectible_levels: the collector level ladder. A wallet's level is the highest level whose min_score is not above its score.
-- collectible_reward_claims: the monthly collector rewards; user_collectible_reward_claims counts how often each wallet claimed one.
-- user_collectible_collection_claims: the bonus and reward of a set a wallet already claimed.
-- All DATETIME values are UTC. Idempotent: every statement can run again without changing data.

CREATE TABLE IF NOT EXISTS collectible_collections (
    id VARCHAR(64) NOT NULL,
    name VARCHAR(100) NOT NULL,
    boost_score INT(11) NOT NULL DEFAULT 0,
    bonus_product_code VARCHAR(64) DEFAULT NULL,
    reward_product_code VARCHAR(64) DEFAULT NULL,
    released_at DATETIME DEFAULT NULL,
    snapshot_at DATETIME DEFAULT NULL,
    sort_order INT(11) NOT NULL DEFAULT 0,
    enabled TINYINT(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS collectible_products (
    product_code VARCHAR(64) NOT NULL,
    collection_id VARCHAR(64) DEFAULT NULL,
    product_type SMALLINT(6) NOT NULL,
    item_type_id VARCHAR(64) NOT NULL,
    score INT(11) NOT NULL DEFAULT 0,
    rarity VARCHAR(32) NOT NULL DEFAULT '',
    pet_figure VARCHAR(255) NOT NULL DEFAULT '',
    figure_set_ids VARCHAR(255) NOT NULL DEFAULT '',
    sort_order INT(11) NOT NULL DEFAULT 0,
    PRIMARY KEY (product_code),
    KEY collectible_products_collection (collection_id),
    CONSTRAINT collectible_products_collection_fk FOREIGN KEY (collection_id) REFERENCES collectible_collections (id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS collectible_wallets (
    address VARCHAR(42) NOT NULL,
    id INT(11) NOT NULL AUTO_INCREMENT,
    user_id INT(11) NOT NULL,
    is_collector TINYINT(1) NOT NULL DEFAULT 0,
    collector_user_id INT(11) GENERATED ALWAYS AS (IF(is_collector = 1, user_id, NULL)) STORED,
    mint_tokens INT(11) NOT NULL DEFAULT 0,
    highest_score INT(11) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (address),
    UNIQUE KEY collectible_wallets_id (id),
    UNIQUE KEY collectible_wallets_one_collector (collector_user_id),
    KEY collectible_wallets_user (user_id),
    CONSTRAINT collectible_wallets_user_fk FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS user_collectibles (
    id INT(11) NOT NULL AUTO_INCREMENT,
    wallet_address VARCHAR(42) NOT NULL,
    product_code VARCHAR(64) NOT NULL,
    serial_number INT(11) NOT NULL,
    source ENUM('shop','mint','collection_reward','collection_bonus','reward_claim','staff') NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY user_collectibles_serial (product_code, serial_number),
    KEY user_collectibles_wallet (wallet_address),
    CONSTRAINT user_collectibles_wallet_fk FOREIGN KEY (wallet_address) REFERENCES collectible_wallets (address) ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT user_collectibles_product_fk FOREIGN KEY (product_code) REFERENCES collectible_products (product_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS collectible_shop_offers (
    product_code VARCHAR(64) NOT NULL,
    emerald_price INT(11) NOT NULL,
    is_featured TINYINT(1) NOT NULL DEFAULT 0,
    is_limited TINYINT(1) NOT NULL DEFAULT 0,
    mint_limit INT(11) NOT NULL DEFAULT -1,
    sort_order INT(11) NOT NULL DEFAULT 0,
    enabled TINYINT(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (product_code),
    CONSTRAINT collectible_shop_offers_product_fk FOREIGN KEY (product_code) REFERENCES collectible_products (product_code) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS collectible_mint_token_offers (
    id INT(11) NOT NULL AUTO_INCREMENT,
    product_code VARCHAR(64) NOT NULL,
    silver_price INT(11) NOT NULL,
    amount_tokens INT(11) NOT NULL,
    sort_order INT(11) NOT NULL DEFAULT 0,
    enabled TINYINT(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS collectible_mintable_items (
    id INT(11) NOT NULL AUTO_INCREMENT,
    item_type ENUM('s','i') NOT NULL,
    sprite_id INT(11) NOT NULL,
    product_code VARCHAR(64) NOT NULL,
    token_price INT(11) NOT NULL,
    starts_at DATETIME DEFAULT NULL,
    ends_at DATETIME DEFAULT NULL,
    region_locked TINYINT(1) NOT NULL DEFAULT 0,
    limited_edition TINYINT(1) NOT NULL DEFAULT 0,
    enabled TINYINT(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (id),
    UNIQUE KEY collectible_mintable_items_sprite (item_type, sprite_id),
    CONSTRAINT collectible_mintable_items_product_fk FOREIGN KEY (product_code) REFERENCES collectible_products (product_code) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS collectible_levels (
    level INT(11) NOT NULL,
    min_score INT(11) NOT NULL,
    PRIMARY KEY (level)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS collectible_reward_claims (
    claim_id VARCHAR(64) NOT NULL,
    product_code VARCHAR(64) NOT NULL,
    set_id VARCHAR(64) NOT NULL,
    set_name VARCHAR(100) NOT NULL,
    min_level INT(11) NOT NULL DEFAULT 1,
    claim_limit INT(11) NOT NULL DEFAULT 1,
    valid_from DATETIME NOT NULL,
    valid_to DATETIME NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (claim_id),
    CONSTRAINT collectible_reward_claims_product_fk FOREIGN KEY (product_code) REFERENCES collectible_products (product_code) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS user_collectible_reward_claims (
    wallet_address VARCHAR(42) NOT NULL,
    claim_id VARCHAR(64) NOT NULL,
    claimed_amount INT(11) NOT NULL DEFAULT 0,
    PRIMARY KEY (wallet_address, claim_id),
    KEY user_collectible_reward_claims_claim (claim_id),
    CONSTRAINT user_collectible_reward_claims_wallet_fk FOREIGN KEY (wallet_address) REFERENCES collectible_wallets (address) ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT user_collectible_reward_claims_claim_fk FOREIGN KEY (claim_id) REFERENCES collectible_reward_claims (claim_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS user_collectible_collection_claims (
    wallet_address VARCHAR(42) NOT NULL,
    collection_id VARCHAR(64) NOT NULL,
    kind ENUM('bonus','reward') NOT NULL,
    claimed_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (wallet_address, collection_id, kind),
    KEY user_collectible_collection_claims_collection (collection_id),
    CONSTRAINT user_collectible_collection_claims_wallet_fk FOREIGN KEY (wallet_address) REFERENCES collectible_wallets (address) ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT user_collectible_collection_claims_collection_fk FOREIGN KEY (collection_id) REFERENCES collectible_collections (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT IGNORE INTO collectible_levels (level, min_score) VALUES
(1, 0), (2, 100), (3, 250), (4, 500), (5, 1000), (6, 1750), (7, 2750), (8, 4000), (9, 5500), (10, 7500),
(11, 10000), (12, 13000), (13, 16500), (14, 20500), (15, 25000), (16, 30000), (17, 36000), (18, 43000), (19, 51000), (20, 60000);

INSERT IGNORE INTO server_settings (`key`, `value`) VALUES ('collectibles.minting.enabled', '1'), ('collectibles.transfer.fee', '0');
