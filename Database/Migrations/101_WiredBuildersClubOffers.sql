-- Habbo began selling nine existing Wired boxes on 2026-10-20 (Observed on hhus catalog 2026-10-20, AIR WIN63-202610201032-107964540,
-- NORMAL and BUILDERS_CLUB catalogues): Set Highlight, Set Visibility, Room Initialized, Pointer Variable, Import, Share, the :chooser
-- disabler, Label and Leaderboard. This sells them as Habbo does: under Habbo's offer ids, at Habbo's prices, in Habbo's page order.
-- - Set Highlight and Set Visibility take their new names (they were Set Furni Highlight and Set Furni Transparency).
-- - Migration 100 hid six of them on the staff page; they leave it. Boxes Habbo still does not sell stay there.
-- - An offer that migration 81 created under a local id gets Habbo's id (page offers, products and promotions follow); a missing one is created.
-- - New Additions, Wired Triggers, Wired Effects, Variables, Variable Add-Ons, Extras, Leaderboards and Configuration Items list their
--   offers in Habbo's order in both catalogues. Offers that Habbo does not list on those pages keep their place.
-- Idempotent: reruns end in the same state.

START TRANSACTION;

-- 1. Names.
UPDATE furniture SET public_name = 'WIRED Effect: Set Highlight', name = 'WIRED Effect: Set Highlight'
WHERE item_name = 'wf_act_set_highlight' AND type = 's';
UPDATE furniture SET public_name = 'WIRED Effect: Set Visibility', name = 'WIRED Effect: Set Visibility'
WHERE item_name = 'wf_act_set_visibility' AND type = 's';

-- 2. The nine offers: Habbo's id, key and credits, the furni they sell, and the local offer of the same key that is renumbered.
CREATE TEMPORARY TABLE wired_target_101 (
    offer_key VARCHAR(100) NOT NULL PRIMARY KEY,
    official_id INT NOT NULL,
    cost_credits INT UNSIGNED NOT NULL,
    furniture_id INT UNSIGNED NULL,
    local_id INT NULL,
    offer_id INT NULL
) ENGINE=MEMORY;
INSERT INTO wired_target_101 (offer_key, official_id, cost_credits) VALUES
    ('wf_act_set_highlight', 35251, 7),
    ('wf_act_set_visibility', 35252, 7),
    ('wf_trg_room_initializes', 35257, 5),
    ('wf_var_pointer', 35259, 6),
    ('wf_xtra_var_import', 35262, 6),
    ('wf_xtra_var_share', 35264, 6),
    ('conf_chooser_disable', 35246, 5),
    ('wf_label', 35253, 3),
    ('wf_xtra_var_leaderboard', 35263, 15);

UPDATE wired_target_101 t JOIN furniture f ON f.item_name = t.offer_key AND f.type = 's' SET t.furniture_id = f.id;
UPDATE wired_target_101 t JOIN catalog_offers o ON o.id = t.official_id AND o.localization_key = t.offer_key SET t.offer_id = o.id;

-- A local offer with the key that sells the furni once is renumbered, unless Habbo's id is taken by another offer.
UPDATE wired_target_101 t SET t.local_id = (
    SELECT MIN(o.id) FROM catalog_offers o
    JOIN catalog_offer_products op ON op.offer_id = o.id AND op.product_type = 'furni' AND op.furniture_id = t.furniture_id AND op.amount = 1
    WHERE o.localization_key = t.offer_key AND o.id <> t.official_id)
WHERE t.offer_id IS NULL AND t.furniture_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM catalog_offers o WHERE o.id = t.official_id);

UPDATE catalog_promotions p JOIN wired_target_101 t ON p.offer_id = t.local_id SET p.offer_id = t.official_id;
UPDATE catalog_offers o JOIN wired_target_101 t ON o.id = t.local_id SET o.id = t.official_id;
UPDATE wired_target_101 t SET t.offer_id = t.official_id WHERE t.local_id IS NOT NULL;

-- What is still missing is created under Habbo's id.
INSERT INTO catalog_offers (id, localization_key, cost_credits, cost_points, points_type, club_level, bulk_purchase, enabled, preview_image)
SELECT t.official_id, t.offer_key, t.cost_credits, 0, 0, 0, 1, 1, ''
FROM wired_target_101 t
WHERE t.offer_id IS NULL AND t.furniture_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM catalog_offers o WHERE o.id = t.official_id);
INSERT INTO catalog_offer_products (offer_id, position, product_type, furniture_id, amount, extra_param)
SELECT t.official_id, 0, 'furni', t.furniture_id, 1, ''
FROM wired_target_101 t
WHERE t.offer_id IS NULL AND t.furniture_id IS NOT NULL
  AND EXISTS (SELECT 1 FROM catalog_offers o WHERE o.id = t.official_id AND o.localization_key = t.offer_key)
  AND NOT EXISTS (SELECT 1 FROM catalog_offer_products op WHERE op.offer_id = t.official_id);
UPDATE wired_target_101 t JOIN catalog_offers o ON o.id = t.official_id AND o.localization_key = t.offer_key SET t.offer_id = o.id
WHERE t.offer_id IS NULL;

UPDATE catalog_offers o JOIN wired_target_101 t ON t.offer_id = o.id
SET o.cost_credits = t.cost_credits, o.cost_points = 0, o.points_type = 0, o.club_level = 0, o.bulk_purchase = 1, o.enabled = 1;

-- 3. The released boxes leave the staff-only page of migration 100.
DELETE po FROM catalog_page_offers po
JOIN catalog_pages p ON p.id = po.page_id AND p.link = 'staff_wired'
JOIN wired_target_101 t ON t.offer_id = po.offer_id;

-- 4. Page order, (catalogue, page link, position, offer key) as Habbo lists it. A page that this hotel lacks is skipped.
CREATE TEMPORARY TABLE wired_order_101 (
    catalog_mode VARCHAR(16) NOT NULL,
    page_link VARCHAR(128) NOT NULL,
    position INT NOT NULL,
    offer_key VARCHAR(100) NOT NULL,
    page_id INT NULL,
    offer_id INT NULL,
    PRIMARY KEY (catalog_mode, page_link, position)
) ENGINE=MEMORY;
INSERT INTO wired_order_101 (catalog_mode, page_link, position, offer_key) VALUES
    ('NORMAL', 'wired_new', 0, 'wf_trg_room_initializes'),
    ('NORMAL', 'wired_new', 1, 'wf_act_set_visibility'),
    ('NORMAL', 'wired_new', 2, 'wf_act_set_highlight'),
    ('NORMAL', 'wired_new', 3, 'wf_xtra_var_share'),
    ('NORMAL', 'wired_new', 4, 'wf_xtra_var_import'),
    ('NORMAL', 'wired_new', 5, 'wf_var_pointer'),
    ('NORMAL', 'wired_new', 6, 'wf_xtra_var_web_api'),
    ('NORMAL', 'wired_new', 7, 'wf_xtra_var_web_api_deal1'),
    ('NORMAL', 'wired_new', 8, 'conf_chooser_disable'),
    ('NORMAL', 'wired_new', 9, 'wf_xtra_var_leaderboard'),
    ('NORMAL', 'wired_new', 10, 'wf_label'),
    ('NORMAL', 'wired_new', 11, 'wf_xtra_varfx_hp'),
    ('NORMAL', 'wired_new', 12, 'wf_xtra_varfx_prog'),
    ('NORMAL', 'wired_new', 13, 'wf_xtra_varfx_status'),
    ('NORMAL', 'wired_new', 14, 'wf_xtra_varfx_levelling'),
    ('NORMAL', 'wired_new', 15, 'wf_xtra_varfx_number'),
    ('NORMAL', 'wired_new', 16, 'wf_xtra_varfx_boss'),
    ('NORMAL', 'wired_triggers', 0, 'wf_trg_walks_on_furni'),
    ('NORMAL', 'wired_triggers', 1, 'wf_trg_walks_off_furni'),
    ('NORMAL', 'wired_triggers', 2, 'wf_trg_says_something'),
    ('NORMAL', 'wired_triggers', 3, 'wf_trg_state_changed'),
    ('NORMAL', 'wired_triggers', 4, 'wf_trg_stuff_state'),
    ('NORMAL', 'wired_triggers', 5, 'wf_trg_enter_room'),
    ('NORMAL', 'wired_triggers', 6, 'wf_trg_leave_room'),
    ('NORMAL', 'wired_triggers', 7, 'wf_trg_click_furni'),
    ('NORMAL', 'wired_triggers', 8, 'wf_trg_click_user'),
    ('NORMAL', 'wired_triggers', 9, 'wf_trg_click_tile'),
    ('NORMAL', 'wired_triggers', 10, 'wf_trg_periodically'),
    ('NORMAL', 'wired_triggers', 11, 'wf_trg_period_short'),
    ('NORMAL', 'wired_triggers', 12, 'wf_trg_period_long'),
    ('NORMAL', 'wired_triggers', 13, 'wf_trg_user_performs_action'),
    ('NORMAL', 'wired_triggers', 14, 'wf_trg_collision'),
    ('NORMAL', 'wired_triggers', 15, 'wf_trg_recv_signal'),
    ('NORMAL', 'wired_triggers', 16, 'wf_trg_clock_counter'),
    ('NORMAL', 'wired_triggers', 17, 'wf_trg_game_ends'),
    ('NORMAL', 'wired_triggers', 18, 'wf_trg_game_starts'),
    ('NORMAL', 'wired_triggers', 19, 'wf_trg_score_achieved'),
    ('NORMAL', 'wired_triggers', 20, 'wf_trg_room_initializes'),
    ('NORMAL', 'wired_effects', 0, 'wf_act_toggle_state'),
    ('NORMAL', 'wired_effects', 1, 'wf_act_move_rotate'),
    ('NORMAL', 'wired_effects', 2, 'wf_act_move_rotate_user'),
    ('NORMAL', 'wired_effects', 3, 'wf_act_match_to_sshot'),
    ('NORMAL', 'wired_effects', 4, 'wf_act_teleport_to'),
    ('NORMAL', 'wired_effects', 5, 'wf_act_show_message'),
    ('NORMAL', 'wired_effects', 6, 'wf_act_toggle_to_rnd'),
    ('NORMAL', 'wired_effects', 7, 'wf_act_call_stacks'),
    ('NORMAL', 'wired_effects', 8, 'wf_act_control_clock'),
    ('NORMAL', 'wired_effects', 9, 'wf_act_adjust_clock'),
    ('NORMAL', 'wired_effects', 10, 'wf_act_send_signal'),
    ('NORMAL', 'wired_effects', 11, 'wf_act_teleport_to_room'),
    ('NORMAL', 'wired_effects', 12, 'wf_act_set_altitude'),
    ('NORMAL', 'wired_effects', 13, 'wf_act_move_to_dir'),
    ('NORMAL', 'wired_effects', 14, 'wf_act_furni_to_furni'),
    ('NORMAL', 'wired_effects', 15, 'wf_act_furni_to_user'),
    ('NORMAL', 'wired_effects', 16, 'wf_act_user_to_furni'),
    ('NORMAL', 'wired_effects', 17, 'wf_act_rel_mov'),
    ('NORMAL', 'wired_effects', 18, 'wf_act_place_furni'),
    ('NORMAL', 'wired_effects', 19, 'wf_act_remove_furni'),
    ('NORMAL', 'wired_effects', 20, 'wf_act_move_furni_as_group'),
    ('NORMAL', 'wired_effects', 21, 'wf_act_chase'),
    ('NORMAL', 'wired_effects', 22, 'wf_act_set_visibility'),
    ('NORMAL', 'wired_effects', 23, 'wf_act_set_highlight'),
    ('NORMAL', 'wired_effects', 24, 'wf_act_join_team'),
    ('NORMAL', 'wired_effects', 25, 'wf_act_leave_team'),
    ('NORMAL', 'wired_effects', 26, 'wf_act_give_score'),
    ('NORMAL', 'wired_effects', 27, 'wf_act_give_score_tm'),
    ('NORMAL', 'wired_effects', 28, 'wf_act_freeze'),
    ('NORMAL', 'wired_effects', 29, 'wf_act_unfreeze'),
    ('NORMAL', 'wired_effects', 30, 'wf_act_click_conf'),
    ('NORMAL', 'wired_effects', 31, 'wf_act_kick_user'),
    ('NORMAL', 'wired_effects', 32, 'wf_act_bot_give_handitem'),
    ('NORMAL', 'wired_effects', 33, 'wf_act_bot_move'),
    ('NORMAL', 'wired_effects', 34, 'wf_act_bot_talk'),
    ('NORMAL', 'wired_effects', 35, 'wf_act_bot_talk_to_avatar'),
    ('NORMAL', 'wired_effects', 36, 'wf_act_bot_clothes'),
    ('NORMAL', 'wired_effects', 37, 'wf_act_log'),
    ('NORMAL', 'wired_effects', 38, 'wf_act_neg_call_stacks'),
    ('NORMAL', 'wired_effects', 39, 'wf_act_neg_send_signal'),
    ('NORMAL', 'wired_effects', 40, 'wf_act_neg_log'),
    ('NORMAL', 'wired_variables', 0, 'wf_var_user'),
    ('NORMAL', 'wired_variables', 1, 'wf_var_furni'),
    ('NORMAL', 'wired_variables', 2, 'wf_var_room'),
    ('NORMAL', 'wired_variables', 3, 'wf_var_context'),
    ('NORMAL', 'wired_variables', 4, 'wf_var_reference'),
    ('NORMAL', 'wired_variables', 5, 'wf_var_echo'),
    ('NORMAL', 'wired_variables', 6, 'wf_var_pointer'),
    ('NORMAL', 'wired_variables_addons', 0, 'wf_xtra_var_text_connector'),
    ('NORMAL', 'wired_variables_addons', 1, 'wf_xtra_var_lvlup_system'),
    ('NORMAL', 'wired_variables_addons', 2, 'wf_xtra_var_time_util'),
    ('NORMAL', 'wired_variables_addons', 3, 'wf_xtra_var_leaderboard'),
    ('NORMAL', 'wired_variables_addons', 4, 'wf_xtra_var_share'),
    ('NORMAL', 'wired_variables_addons', 5, 'wf_xtra_var_import'),
    ('NORMAL', 'wired_variables_addons', 6, 'wf_xtra_var_web_api'),
    ('NORMAL', 'wired_variables_addons', 7, 'wf_xtra_var_web_api_deal1'),
    ('NORMAL', 'wired_extras', 0, 'wf_game_upcounter1'),
    ('NORMAL', 'wired_extras', 1, 'wf_game_upcounter2'),
    ('NORMAL', 'wired_extras', 2, 'wf_upcounter1'),
    ('NORMAL', 'wired_extras', 3, 'wf_upcounter2'),
    ('NORMAL', 'wired_extras', 4, 'wf_antenna1'),
    ('NORMAL', 'wired_extras', 5, 'wf_antenna2'),
    ('NORMAL', 'wired_extras', 6, 'wf_room_linker'),
    ('NORMAL', 'wired_extras', 7, 'wf_label'),
    ('NORMAL', 'wired_leaderboard', 0, 'highscore_fastesttime_alltime'),
    ('NORMAL', 'wired_leaderboard', 1, 'highscore_fastesttime_daily'),
    ('NORMAL', 'wired_leaderboard', 2, 'highscore_fastesttime_weekly'),
    ('NORMAL', 'wired_leaderboard', 3, 'highscore_fastesttime_monthly'),
    ('NORMAL', 'wired_leaderboard', 4, 'highscore_longesttime_alltime'),
    ('NORMAL', 'wired_leaderboard', 5, 'highscore_longesttime_daily'),
    ('NORMAL', 'wired_leaderboard', 6, 'highscore_longesttime_weekly'),
    ('NORMAL', 'wired_leaderboard', 7, 'highscore_longesttime_monthly'),
    ('NORMAL', 'wired_leaderboard', 8, 'highscore_classic_alltime'),
    ('NORMAL', 'wired_leaderboard', 9, 'highscore_classic_daily'),
    ('NORMAL', 'wired_leaderboard', 10, 'highscore_classic_weekly'),
    ('NORMAL', 'wired_leaderboard', 11, 'highscore_classic_monthly'),
    ('NORMAL', 'wired_leaderboard', 12, 'highscore_perteam_alltime'),
    ('NORMAL', 'wired_leaderboard', 13, 'highscore_perteam_daily'),
    ('NORMAL', 'wired_leaderboard', 14, 'highscore_perteam_weekly'),
    ('NORMAL', 'wired_leaderboard', 15, 'highscore_perteam_monthly'),
    ('NORMAL', 'wired_leaderboard', 16, 'highscore_mostwin_alltime'),
    ('NORMAL', 'wired_leaderboard', 17, 'highscore_mostwin_daily'),
    ('NORMAL', 'wired_leaderboard', 18, 'highscore_mostwin_weekly'),
    ('NORMAL', 'wired_leaderboard', 19, 'highscore_mostwin_monthly'),
    ('NORMAL', 'wired_leaderboard', 20, 'wf_xtra_var_leaderboard'),
    ('NORMAL', 'configitems', 0, 'conf_queue_speed'),
    ('NORMAL', 'configitems', 1, 'conf_invis_control'),
    ('NORMAL', 'configitems', 2, 'conf_handitem_block'),
    ('NORMAL', 'configitems', 3, 'conf_wired_disable'),
    ('NORMAL', 'configitems', 4, 'conf_area_hide'),
    ('NORMAL', 'configitems', 5, 'conf_chooser_disable'),
    ('BUILDERS_CLUB', 'wired_new', 0, 'wf_trg_room_initializes'),
    ('BUILDERS_CLUB', 'wired_new', 1, 'wf_act_set_visibility'),
    ('BUILDERS_CLUB', 'wired_new', 2, 'wf_act_set_highlight'),
    ('BUILDERS_CLUB', 'wired_new', 3, 'wf_xtra_var_share'),
    ('BUILDERS_CLUB', 'wired_new', 4, 'wf_xtra_var_import'),
    ('BUILDERS_CLUB', 'wired_new', 5, 'wf_var_pointer'),
    ('BUILDERS_CLUB', 'wired_new', 6, 'conf_chooser_disable'),
    ('BUILDERS_CLUB', 'wired_new', 7, 'wf_xtra_var_leaderboard'),
    ('BUILDERS_CLUB', 'wired_new', 8, 'wf_label'),
    ('BUILDERS_CLUB', 'wired_new', 9, 'wf_xtra_varfx_hp'),
    ('BUILDERS_CLUB', 'wired_new', 10, 'wf_xtra_varfx_prog'),
    ('BUILDERS_CLUB', 'wired_new', 11, 'wf_xtra_varfx_status'),
    ('BUILDERS_CLUB', 'wired_new', 12, 'wf_xtra_varfx_levelling'),
    ('BUILDERS_CLUB', 'wired_new', 13, 'wf_xtra_varfx_number'),
    ('BUILDERS_CLUB', 'wired_new', 14, 'wf_xtra_varfx_boss'),
    ('BUILDERS_CLUB', 'wired_triggers', 0, 'wf_trg_walks_on_furni'),
    ('BUILDERS_CLUB', 'wired_triggers', 1, 'wf_trg_walks_off_furni'),
    ('BUILDERS_CLUB', 'wired_triggers', 2, 'wf_trg_says_something'),
    ('BUILDERS_CLUB', 'wired_triggers', 3, 'wf_trg_state_changed'),
    ('BUILDERS_CLUB', 'wired_triggers', 4, 'wf_trg_stuff_state'),
    ('BUILDERS_CLUB', 'wired_triggers', 5, 'wf_trg_enter_room'),
    ('BUILDERS_CLUB', 'wired_triggers', 6, 'wf_trg_leave_room'),
    ('BUILDERS_CLUB', 'wired_triggers', 7, 'wf_trg_click_furni'),
    ('BUILDERS_CLUB', 'wired_triggers', 8, 'wf_trg_click_user'),
    ('BUILDERS_CLUB', 'wired_triggers', 9, 'wf_trg_click_tile'),
    ('BUILDERS_CLUB', 'wired_triggers', 10, 'wf_trg_periodically'),
    ('BUILDERS_CLUB', 'wired_triggers', 11, 'wf_trg_period_short'),
    ('BUILDERS_CLUB', 'wired_triggers', 12, 'wf_trg_period_long'),
    ('BUILDERS_CLUB', 'wired_triggers', 13, 'wf_trg_user_performs_action'),
    ('BUILDERS_CLUB', 'wired_triggers', 14, 'wf_trg_collision'),
    ('BUILDERS_CLUB', 'wired_triggers', 15, 'wf_trg_recv_signal'),
    ('BUILDERS_CLUB', 'wired_triggers', 16, 'wf_trg_clock_counter'),
    ('BUILDERS_CLUB', 'wired_triggers', 17, 'wf_trg_game_ends'),
    ('BUILDERS_CLUB', 'wired_triggers', 18, 'wf_trg_game_starts'),
    ('BUILDERS_CLUB', 'wired_triggers', 19, 'wf_trg_score_achieved'),
    ('BUILDERS_CLUB', 'wired_triggers', 20, 'wf_trg_room_initializes'),
    ('BUILDERS_CLUB', 'wired_effects', 0, 'wf_act_toggle_state'),
    ('BUILDERS_CLUB', 'wired_effects', 1, 'wf_act_move_rotate'),
    ('BUILDERS_CLUB', 'wired_effects', 2, 'wf_act_move_rotate_user'),
    ('BUILDERS_CLUB', 'wired_effects', 3, 'wf_act_match_to_sshot'),
    ('BUILDERS_CLUB', 'wired_effects', 4, 'wf_act_teleport_to'),
    ('BUILDERS_CLUB', 'wired_effects', 5, 'wf_act_show_message'),
    ('BUILDERS_CLUB', 'wired_effects', 6, 'wf_act_toggle_to_rnd'),
    ('BUILDERS_CLUB', 'wired_effects', 7, 'wf_act_call_stacks'),
    ('BUILDERS_CLUB', 'wired_effects', 8, 'wf_act_control_clock'),
    ('BUILDERS_CLUB', 'wired_effects', 9, 'wf_act_adjust_clock'),
    ('BUILDERS_CLUB', 'wired_effects', 10, 'wf_act_send_signal'),
    ('BUILDERS_CLUB', 'wired_effects', 11, 'wf_act_teleport_to_room'),
    ('BUILDERS_CLUB', 'wired_effects', 12, 'wf_act_set_altitude'),
    ('BUILDERS_CLUB', 'wired_effects', 13, 'wf_act_move_to_dir'),
    ('BUILDERS_CLUB', 'wired_effects', 14, 'wf_act_furni_to_furni'),
    ('BUILDERS_CLUB', 'wired_effects', 15, 'wf_act_furni_to_user'),
    ('BUILDERS_CLUB', 'wired_effects', 16, 'wf_act_user_to_furni'),
    ('BUILDERS_CLUB', 'wired_effects', 17, 'wf_act_rel_mov'),
    ('BUILDERS_CLUB', 'wired_effects', 18, 'wf_act_place_furni'),
    ('BUILDERS_CLUB', 'wired_effects', 19, 'wf_act_remove_furni'),
    ('BUILDERS_CLUB', 'wired_effects', 20, 'wf_act_move_furni_as_group'),
    ('BUILDERS_CLUB', 'wired_effects', 21, 'wf_act_chase'),
    ('BUILDERS_CLUB', 'wired_effects', 22, 'wf_act_set_visibility'),
    ('BUILDERS_CLUB', 'wired_effects', 23, 'wf_act_set_highlight'),
    ('BUILDERS_CLUB', 'wired_effects', 24, 'wf_act_join_team'),
    ('BUILDERS_CLUB', 'wired_effects', 25, 'wf_act_leave_team'),
    ('BUILDERS_CLUB', 'wired_effects', 26, 'wf_act_give_score'),
    ('BUILDERS_CLUB', 'wired_effects', 27, 'wf_act_give_score_tm'),
    ('BUILDERS_CLUB', 'wired_effects', 28, 'wf_act_freeze'),
    ('BUILDERS_CLUB', 'wired_effects', 29, 'wf_act_unfreeze'),
    ('BUILDERS_CLUB', 'wired_effects', 30, 'wf_act_click_conf'),
    ('BUILDERS_CLUB', 'wired_effects', 31, 'wf_act_kick_user'),
    ('BUILDERS_CLUB', 'wired_effects', 32, 'wf_act_bot_give_handitem'),
    ('BUILDERS_CLUB', 'wired_effects', 33, 'wf_act_bot_move'),
    ('BUILDERS_CLUB', 'wired_effects', 34, 'wf_act_bot_talk'),
    ('BUILDERS_CLUB', 'wired_effects', 35, 'wf_act_bot_talk_to_avatar'),
    ('BUILDERS_CLUB', 'wired_effects', 36, 'wf_act_bot_clothes'),
    ('BUILDERS_CLUB', 'wired_effects', 37, 'wf_act_log'),
    ('BUILDERS_CLUB', 'wired_effects', 38, 'wf_act_neg_call_stacks'),
    ('BUILDERS_CLUB', 'wired_effects', 39, 'wf_act_neg_send_signal'),
    ('BUILDERS_CLUB', 'wired_effects', 40, 'wf_act_neg_log'),
    ('BUILDERS_CLUB', 'wired_variables', 0, 'wf_var_user'),
    ('BUILDERS_CLUB', 'wired_variables', 1, 'wf_var_furni'),
    ('BUILDERS_CLUB', 'wired_variables', 2, 'wf_var_room'),
    ('BUILDERS_CLUB', 'wired_variables', 3, 'wf_var_context'),
    ('BUILDERS_CLUB', 'wired_variables', 4, 'wf_var_reference'),
    ('BUILDERS_CLUB', 'wired_variables', 5, 'wf_var_echo'),
    ('BUILDERS_CLUB', 'wired_variables', 6, 'wf_var_pointer'),
    ('BUILDERS_CLUB', 'wired_variables_addons', 0, 'wf_xtra_var_text_connector'),
    ('BUILDERS_CLUB', 'wired_variables_addons', 1, 'wf_xtra_var_lvlup_system'),
    ('BUILDERS_CLUB', 'wired_variables_addons', 2, 'wf_xtra_var_time_util'),
    ('BUILDERS_CLUB', 'wired_variables_addons', 3, 'wf_xtra_var_leaderboard'),
    ('BUILDERS_CLUB', 'wired_variables_addons', 4, 'wf_xtra_var_share'),
    ('BUILDERS_CLUB', 'wired_variables_addons', 5, 'wf_xtra_var_import'),
    ('BUILDERS_CLUB', 'wired_extras', 0, 'wf_game_upcounter1'),
    ('BUILDERS_CLUB', 'wired_extras', 1, 'wf_game_upcounter2'),
    ('BUILDERS_CLUB', 'wired_extras', 2, 'wf_upcounter1'),
    ('BUILDERS_CLUB', 'wired_extras', 3, 'wf_upcounter2'),
    ('BUILDERS_CLUB', 'wired_extras', 4, 'wf_antenna1'),
    ('BUILDERS_CLUB', 'wired_extras', 5, 'wf_antenna2'),
    ('BUILDERS_CLUB', 'wired_extras', 6, 'wf_label'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 0, 'highscore_fastesttime_alltime'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 1, 'highscore_fastesttime_daily'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 2, 'highscore_fastesttime_weekly'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 3, 'highscore_fastesttime_monthly'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 4, 'highscore_longesttime_alltime'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 5, 'highscore_longesttime_daily'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 6, 'highscore_longesttime_weekly'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 7, 'highscore_longesttime_monthly'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 8, 'highscore_classic_alltime'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 9, 'highscore_classic_daily'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 10, 'highscore_classic_weekly'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 11, 'highscore_classic_monthly'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 12, 'highscore_perteam_alltime'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 13, 'highscore_perteam_daily'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 14, 'highscore_perteam_weekly'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 15, 'highscore_perteam_monthly'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 16, 'highscore_mostwin_alltime'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 17, 'highscore_mostwin_daily'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 18, 'highscore_mostwin_weekly'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 19, 'highscore_mostwin_monthly'),
    ('BUILDERS_CLUB', 'wired_leaderboard', 20, 'wf_xtra_var_leaderboard'),
    ('BUILDERS_CLUB', 'configitems', 0, 'conf_queue_speed'),
    ('BUILDERS_CLUB', 'configitems', 1, 'conf_invis_control'),
    ('BUILDERS_CLUB', 'configitems', 2, 'conf_handitem_block'),
    ('BUILDERS_CLUB', 'configitems', 3, 'conf_wired_disable'),
    ('BUILDERS_CLUB', 'configitems', 4, 'conf_area_hide'),
    ('BUILDERS_CLUB', 'configitems', 5, 'conf_chooser_disable');

UPDATE wired_order_101 w JOIN catalog_pages c ON c.catalog_mode = w.catalog_mode AND c.link = w.page_link SET w.page_id = c.id;
UPDATE wired_order_101 w JOIN wired_target_101 t ON t.offer_key = w.offer_key SET w.offer_id = t.offer_id;
UPDATE wired_order_101 w SET w.offer_id = (
    SELECT MIN(o.id) FROM catalog_page_offers po JOIN catalog_offers o ON o.id = po.offer_id
    WHERE po.page_id = w.page_id AND o.localization_key = w.offer_key)
WHERE w.offer_id IS NULL AND w.page_id IS NOT NULL;

INSERT INTO catalog_page_offers (page_id, offer_id, position)
SELECT w.page_id, w.offer_id, MIN(w.position) FROM wired_order_101 w
WHERE w.page_id IS NOT NULL AND w.offer_id IS NOT NULL
GROUP BY w.page_id, w.offer_id
ON DUPLICATE KEY UPDATE position = VALUES(position);

DROP TEMPORARY TABLE wired_order_101;
DROP TEMPORARY TABLE wired_target_101;

COMMIT;
