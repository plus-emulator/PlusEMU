ALTER TABLE recycler_settings ADD COLUMN IF NOT EXISTS cost_duckets INT NOT NULL DEFAULT 0;
ALTER TABLE recycler_prizes MODIFY COLUMN item_id INT UNSIGNED NULL;
ALTER TABLE recycler_prizes ADD COLUMN IF NOT EXISTS chat_style_id INT NULL DEFAULT NULL;
ALTER TABLE recycler_prizes ADD UNIQUE INDEX IF NOT EXISTS recycler_level_style(level,chat_style_id);
ALTER TABLE user_presents MODIFY COLUMN base_id INT UNSIGNED NULL;
ALTER TABLE user_presents ADD COLUMN IF NOT EXISTS chat_style_id INT NULL DEFAULT NULL;
ALTER TABLE room_chat_styles ADD COLUMN IF NOT EXISTS requires_ownership BOOL NOT NULL DEFAULT FALSE;
CREATE TABLE IF NOT EXISTS user_purchased_chat_styles (
    user_id INT NOT NULL,
    style_id INT NOT NULL,
    PRIMARY KEY(user_id,style_id),
    CONSTRAINT fk_purchased_chat_user FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_purchased_chat_style FOREIGN KEY(style_id) REFERENCES room_chat_styles(id)
) ENGINE=InnoDB;
ALTER TABLE users MODIFY COLUMN bubble_id INT NOT NULL DEFAULT 0;
