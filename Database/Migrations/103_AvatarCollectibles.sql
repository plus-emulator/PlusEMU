-- Avatar collectibles: looks staff give with :givecollectible, listed in the avatar editor's Avatar Collectibles tab.
CREATE TABLE IF NOT EXISTS user_nft_wardrobe (
    id INT(11) NOT NULL AUTO_INCREMENT,
    user_id INT(11) NOT NULL,
    figure VARCHAR(255) NOT NULL,
    gender ENUM('M','F') NOT NULL,
    contract_key VARCHAR(64) NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    KEY user_nft_wardrobe_user_id (user_id),
    CONSTRAINT user_nft_wardrobe_user_fk FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- The collectible a user last put on and the look it replaced. It counts as worn only while users.look still equals its figure.
CREATE TABLE IF NOT EXISTS user_nft_outfits (
    user_id INT(11) NOT NULL,
    nft_id INT(11) NOT NULL,
    fallback_figure VARCHAR(255) NOT NULL,
    fallback_gender ENUM('M','F') NOT NULL,
    PRIMARY KEY (user_id),
    KEY user_nft_outfits_nft_id (nft_id),
    CONSTRAINT user_nft_outfits_user_fk FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
    CONSTRAINT user_nft_outfits_nft_fk FOREIGN KEY (nft_id) REFERENCES user_nft_wardrobe (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Whoever may give badges may give collectibles.
INSERT IGNORE INTO acl_permissions (`key`, category, description, is_orphan)
VALUES ('command.givecollectible', 'command', 'Give a user a collectible avatar of your look.', FALSE);

INSERT IGNORE INTO role_permissions (role_id, permission_key)
SELECT role_id, 'command.givecollectible' FROM role_permissions WHERE permission_key = 'command.givebadge';
