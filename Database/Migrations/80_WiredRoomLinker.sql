-- WIRED Room Linker (wf_room_linker, official sprite 16744) is sold as a linked pair like a teleporter
-- (room_items_tele_links), and "WIRED Effect: Teleport to Room" forwards users to the room its other half stands in.
-- The interaction 'wired_room_linker' makes the catalogue create both halves. Idempotent: reruns change nothing.
UPDATE furniture SET interaction_type = 'wired_room_linker'
WHERE item_name = 'wf_room_linker' AND type = 's'
  AND interaction_type IN ('default', 'default_floor', 'wf_room_linker');
