-- :disablediagonal now toggles, per room, whether avatars may walk diagonally between two
-- blocked tiles that touch at a corner. Every room starts with squeezing off.
ALTER TABLE rooms ADD COLUMN IF NOT EXISTS diagonal_squeeze TINYINT(1) NOT NULL DEFAULT 0;
