-- A bot or pet stands at the height of the tile under it (0.2 on a colour tile), so bots.z keeps its decimals like items.z.
-- Idempotent: reruns leave the column and the stored heights as they are.

ALTER TABLE bots MODIFY COLUMN z double NOT NULL DEFAULT 0;
