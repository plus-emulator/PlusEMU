-- A room without a Wired settings row now has the default of a new room: only the owner, staff and group admins may modify and inspect.
-- Every room that exists now keeps its current access (users with rights and group admins). Saved rows stay as they are.

INSERT IGNORE INTO room_wired_settings (room_id, inspect_mask, modify_mask, timezone)
SELECT id, 10, 10, '' FROM rooms;
