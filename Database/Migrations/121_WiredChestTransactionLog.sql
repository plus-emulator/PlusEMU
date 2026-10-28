-- The retained chest transactions become the room and chest transaction log.
ALTER TABLE wired_chest_transactions ADD COLUMN IF NOT EXISTS log_type TINYINT UNSIGNED NOT NULL DEFAULT 0 AFTER source_id;
CREATE INDEX IF NOT EXISTS wired_chest_transactions_room ON wired_chest_transactions(room_id, id);
CREATE INDEX IF NOT EXISTS wired_chest_entries_chest ON wired_chest_transaction_entries(chest_id, transaction_id);
