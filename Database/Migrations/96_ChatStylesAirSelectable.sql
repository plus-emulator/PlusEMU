-- AIR only offers non-system chatstyles_xml styles (RoomChatInputView.createOrUpdateChatStylesView, AIR
-- WIN63-202610151745-318700621 RoomChatInputView.as:356-425). parrot (28), skelestock (32), goat (35), santa (36)
-- and radio (38) are system styles and 18 is not in the library, so they are staff-only like the other system styles.
UPDATE room_chat_styles SET required_permission = 'chat.style.staff', requires_hc = 0 WHERE id IN (18, 28, 32, 35, 36, 38);
