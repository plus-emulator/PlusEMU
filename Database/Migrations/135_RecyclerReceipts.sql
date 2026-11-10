CREATE TABLE IF NOT EXISTS recycler_revisions (
    hash CHAR(64) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
    configuration_json LONGTEXT NOT NULL CHECK(JSON_VALID(configuration_json)),
    created_at DATETIME(6) NOT NULL
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS recycler_operations (
    reference CHAR(36) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
    user_id INT NOT NULL,
    box_item_id INT UNSIGNED NOT NULL UNIQUE,
    configuration_hash CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    prize_id INT NOT NULL,
    prize_level INT NOT NULL,
    prize_item_id INT UNSIGNED NOT NULL,
    chat_style_id INT NOT NULL DEFAULT 0,
    inputs_json LONGTEXT NOT NULL CHECK(JSON_VALID(inputs_json)),
    cost_duckets INT NOT NULL CHECK(cost_duckets>=0),
    balance_after INT NULL,
    created_at DATETIME(6) NOT NULL,
    KEY user_history(user_id,created_at),
    CONSTRAINT recycler_operation_revision FOREIGN KEY(configuration_hash) REFERENCES recycler_revisions(hash)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS recycler_openings (
    reference CHAR(36) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
    operation_reference CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NULL,
    box_item_id INT UNSIGNED NOT NULL UNIQUE,
    user_id INT NOT NULL,
    prize_level INT NOT NULL,
    reward_item_id INT UNSIGNED NULL UNIQUE,
    reward_definition_id INT UNSIGNED NOT NULL,
    chat_style_id INT NOT NULL DEFAULT 0,
    opened_at DATETIME(6) NOT NULL,
    KEY user_history(user_id,opened_at),
    CONSTRAINT recycler_opening_operation FOREIGN KEY(operation_reference) REFERENCES recycler_operations(reference)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS recycler_progress (
    user_id INT PRIMARY KEY,
    recycled_items BIGINT UNSIGNED NOT NULL DEFAULT 0,
    boxes_opened BIGINT UNSIGNED NOT NULL DEFAULT 0,
    recycled_rewards BIGINT UNSIGNED NOT NULL DEFAULT 0,
    rare_prizes BIGINT UNSIGNED NOT NULL DEFAULT 0,
    grand_prizes BIGINT UNSIGNED NOT NULL DEFAULT 0,
    initial_recycled_items INT UNSIGNED NULL,
    initial_boxes_opened INT UNSIGNED NULL,
    initial_recycled_rewards INT UNSIGNED NULL,
    initial_rare_prizes INT UNSIGNED NULL,
    initial_grand_prizes INT UNSIGNED NULL
) ENGINE=InnoDB;
CREATE OR REPLACE TRIGGER recycler_operations_no_update BEFORE UPDATE ON recycler_operations FOR EACH ROW
SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Recycler receipts cannot be changed';
CREATE OR REPLACE TRIGGER recycler_operations_no_delete BEFORE DELETE ON recycler_operations FOR EACH ROW
SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Recycler receipts cannot be deleted';
CREATE OR REPLACE TRIGGER recycler_openings_no_update BEFORE UPDATE ON recycler_openings FOR EACH ROW
SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Recycler openings cannot be changed';
CREATE OR REPLACE TRIGGER recycler_openings_no_delete BEFORE DELETE ON recycler_openings FOR EACH ROW
SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Recycler openings cannot be deleted';
CREATE OR REPLACE TRIGGER recycler_revisions_no_update BEFORE UPDATE ON recycler_revisions FOR EACH ROW
SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Recycler revisions cannot be changed';
CREATE OR REPLACE TRIGGER recycler_revisions_no_delete BEFORE DELETE ON recycler_revisions FOR EACH ROW
SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Recycler revisions cannot be deleted';
