using static Plus.Database.Migrator.SchemaProbe;

namespace Plus.Database.Migrator;

/// <summary>
/// The migrations from before schema_migrations existed, in the order they were written, each with a probe that tells
/// whether a database already has it. A probe is true on any database where its migration ran, even after later migrations
/// changed what it created, and false where it did not run. Only used the first time the emulator meets such a database.
/// New migrations never go here: they are just files in Database/Migrations.
/// </summary>
internal static class LegacyMigrations
{
    /// <param name="Rerunnable">Running it again on a database that has it changes nothing that matters.</param>
    public sealed record Entry(string Id, SchemaProbe Probe, bool Rerunnable);

    public static readonly IReadOnlyList<Entry> All = new Entry[]
    {
        // 1_UpdateRoomEntryTable: drops user_roomvisits.minute and .hour (file:1-3); later files (23, 71, 72) never re-add them.
        new("Updates/1_UpdateRoomEntryTable.sql", NoColumn("user_roomvisits", "minute"), Rerunnable: false),
        // 2_AddMissingRoomColumns: adds rooms.sale_price and rooms.lay_enabled (file:1-3). 25 converts lay_enabled to tinyint(1); sale_price
        // is never touched again.
        new("Updates/2_AddMissingRoomColumns.sql", Column("rooms", "sale_price"), Rerunnable: false),
        // 3_RefactorMessenger: drops messenger_friendships.id, PK (user_one_id, user_two_id), mirrors rows, adds relationship (file:1-5); no
        // later file drops relationship.
        // NOTE: line 6 `DROP TABLE \` user_relationships \`` (spaces inside the backticks) always fails with ERROR 1103, so a runner that
        // stops on the first error fails this file after lines 1-5 have already run.
        new("Updates/3_RefactorMessenger.sql", Column("messenger_friendships", "relationship"), Rerunnable: false),
        // 4_IgnoredUsersTable: drops user_ignores.id, PK becomes (user_id, ignore_id) (file:1-2); 71/72 only touch secondary keys and FKs,
        // the PK stays.
        new("Updates/4_IgnoredUsersTable.sql", PrimaryKey("user_ignores", "user_id", "ignore_id"), Rerunnable: false),
        // 5_RenameUserStatsTable: renames user_stats to user_statistics (file:1-2); later files (44, 69-72) alter it, but it is never
        // renamed or dropped.
        new("Updates/5_RenameUserStatsTable.sql", Table("user_statistics"), Rerunnable: false),
        // 6_AddIsAmbassadorUsersColums: adds users.is_ambassador (file:1). Updates/22_RoleBasedAccessControl reads it (22:546), then drops
        // it (22:742), so roles implies 6 ran.
        new("Updates/6_AddIsAmbassadorUsersColums.sql", Any(Column("users", "is_ambassador"), Table("roles")), Rerunnable: false),
        // 7_AddAmbassadorsLogs: creates ambassador_logs (file:1-9). 29 converts timestamp, 70/71 convert it; 69 keeps it on purpose
        // (69:2-3).
        new("Updates/7_AddAmbassadorsLogs.sql", Table("ambassador_logs"), Rerunnable: false),
        // 8_AddBubbleIDToUsersTable: adds users.bubble_id (file:1); no later file touches it.
        new("Updates/8_AddBubbleIDToUsersTable.sql", Column("users", "bubble_id"), Rerunnable: false),
        // 9_ChangeRoomDataToBooleans: CHANGE rooms.allow_pets/allow_pets_eat/room_blocking_disabled/allow_hidewall enum('0','1') -> BOOLEAN
        // in place (file:1-4). 12 keeps them tinyint(1); nothing later changes them.
        new("Updates/9_ChangeRoomDataToBooleans.sql", DataType("rooms", "allow_pets", "tinyint"), Rerunnable: true),
        // 10_UserDataToBooleans: users.online, vip and the user setting enums -> BOOLEAN (file:1-15). Updates/24 moves the settings to
        // users_settings and makes online BOOL again (24:47-53); online is never dropped.
        // NOTE: on the stock 2017 data allow_mimic is enum('1','0'), so the index conversion stores '0' (mimic off) as 2, which still reads
        // as true.
        new("Updates/10_UserDataToBooleans.sql", DataType("users", "online", "tinyint"), Rerunnable: false),
        // 11_ChangeCatalogPagesEnumToBit: DROP and re-ADD catalog_pages.visible/enabled as bit(1) (file:1-4). 52_NormalizeCatalog MODIFYs
        // them to TINYINT(1) and renames page_link to link (52:38-48), so link implies 11 ran.
        // NOTE: drop and re-add: the file resets every page to visible = enabled = 1 (also on its first run).
        new("Updates/11_ChangeCatalogPagesEnumToBit.sql", Any(DataType("catalog_pages", "visible", "bit"), Column("catalog_pages", "link")), Rerunnable: false),
        // 12_ChangeIntToBoolRoomData: renames the four room flags to *_old enums, adds new BOOLEANs at the end of rooms, copies them and
        // drops *_old (file:2-28). Nothing later touches the columns or their order.
        // It applied when: the flags are tinyint AND (allow_pets now sits after spush_enabled, which only 12 does, OR rooms holds rows and
        // none is > 1).
        // The second branch covers hotels installed from a 2026 Original Database.sql, where the columns are tinyint(1) in their original
        // place with 0/1 data.
        // It is false on a 9-only database with values 2 left by 9 (12 then repairs them).
        // NOTE: in strict sql_mode 12 fails on its first ALTER (atomic, nothing changes) if any flag is 0 after 9 (rooms created after 9).
        new("Updates/12_ChangeIntToBoolRoomData.sql", All(DataType("rooms", "allow_pets", "tinyint"), Any( Query("SELECT (SELECT ORDINAL_POSITION FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rooms' AND COLUMN_NAME = 'allow_pets') > (SELECT ORDINAL_POSITION FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'rooms' AND COLUMN_NAME = 'spush_enabled')"), Query("SELECT COUNT(*) > 0 AND MAX(GREATEST(allow_pets, allow_pets_eat, room_blocking_disabled, allow_hidewall)) <= 1 FROM rooms"))), Rerunnable: false),
        // 13_DeletePrimaryKeyAuthTicket: users PK (id, auth_ticket) -> (id) (file:1-3); later files keep the PK (id).
        new("Updates/13_DeletePrimaryKeyAuthTicket.sql", PrimaryKey("users", "id"), Rerunnable: false),
        // 14_AddWiredItemConfigurations: CREATE TABLE IF NOT EXISTS wired_item_configurations (file:2-8); kept by every later file.
        new("14_AddWiredItemConfigurations.sql", Table("wired_item_configurations"), Rerunnable: true),
        // 14_Habbicons: creates habbicon_collections, habbicons and users_habbicons, seeds them, and adds catalog_items.habbicon_id plus the
        // page and offers before 52 (file:11-129). 37 converts users_habbicons.last_used; the tables stay.
        // NOTE: self-contained user variables and PREPARE/EXECUTE (no input needed).
        new("Updates/14_Habbicons.sql", Table("users_habbicons"), Rerunnable: false),
        // 15_AddWiredVariableValues: CREATE TABLE IF NOT EXISTS wired_variable_locks and wired_variable_values (file:3-16); 39/66 alter
        // wired_variable_values, which stays.
        new("15_AddWiredVariableValues.sql", Table("wired_variable_values"), Rerunnable: true),
        // 16_ImportWiredCatalogueData: comments only, no statements (file:1-17).
        // NOTE: does nothing on purpose; it can be recorded as applied without running.
        new("16_ImportWiredCatalogueData.sql", Any(Table("wired_reward_state"), Table("room_wired_settings"), Table("reward_tracks")), Rerunnable: true),
        // 17_AddWiredRewardState: CREATE TABLE IF NOT EXISTS wired_reward_state (file:2-6); kept by every later file.
        new("17_AddWiredRewardState.sql", Table("wired_reward_state"), Rerunnable: true),
        // 18_AddWiredRoomSettings: CREATE TABLE IF NOT EXISTS room_wired_settings (file:2-7); kept by every later file.
        new("18_AddWiredRoomSettings.sql", Table("room_wired_settings"), Rerunnable: true),
        // 15_IntroductionRewardTrack: creates the 7 reward_track*/users_reward_track* tables and seeds the introduction track (file:4-198).
        // 32 deletes the BC task and 35 converts times; the tables stay.
        // Rerun: INSERT IGNORE re-seeds the place_builders_club_furni task that 32 deleted, and badge definitions.
        new("Updates/15_IntroductionRewardTrack.sql", Table("users_reward_track_prizes"), Rerunnable: false),
        // 15_TrustedCamera: creates camera_media, purchases, publications, accounts, competition_entries and camera_quota, and seeds
        // furniture, server_settings and achievements (file:3-75). 73 repairs the data; the tables stay.
        // Rerun: re-adds camera.permission that Updates/22 deletes; after 69 drops server_settings.description, the INSERTs fail.
        new("Updates/15_TrustedCamera.sql", Table("camera_quota"), Rerunnable: false),
        // 16_HabboClubOffers: creates catalog_club_offers and user_club_memberships, seeds 3 offers, sets pages 5/7 to club_buy (file:3-27).
        // Updates/23 and 38 alter both tables; they stay.
        // Rerun: sets pages 5/7 back to club_buy after 18/23 made them vip_buy.
        new("Updates/16_HabboClubOffers.sql", Table("catalog_club_offers"), Rerunnable: false),
        // 19_AddWalkMagicTile: data only, adds or updates furniture tile_walkmagic (interaction tile_walkmagic) and a Room Building
        // catalog_items offer (file:6-27).
        // NOTE: optional user variable @walk_magic_sprite_id (defaults to 14270 when unset).
        new("19_AddWalkMagicTile.sql", Any(All(Table("furniture"), Query("SELECT COUNT(*) > 0 FROM furniture WHERE item_name = 'tile_walkmagic' AND interaction_type = 'tile_walkmagic'")), Column("catalog_pages", "link")), Rerunnable: false),
        // 17_OfficialCatalogStructure: widens catalog_pages, catalog_items and catalog_promotions columns and adds catalog_mode, club_level,
        // preview_image, order_num, position, item_type, offer_id, product_code and expires_at (file:4-25).
        // catalog_mode is dropped by 32 (re-added by BC files) and catalog_items by 52. 41 converts catalog_promotions.expires_at and 69
        // drops only unknown/parent_id, so product_code is the marker that lasts.
        // Rerun: errors after 52: ALTER TABLE catalog_items.
        new("Updates/17_OfficialCatalogStructure.sql", Column("catalog_promotions", "product_code"), Rerunnable: false),
        // 18_ModernHabboClubPages: data only, catalog_pages id 5/7 club_buy -> vip_buy (file:4-12). Updates/23 sets the same layout without
        // conditions (23:20-21).
        // After 52 the column is layout instead of page_layout, so the query is guarded and link/layout counts as applied.
        // NOTE: depends on the hotel's catalog data.
        // Rerun: no data change before 52, but errors after 52: no page_layout column.
        new("Updates/18_ModernHabboClubPages.sql", Any(All(Column("catalog_pages", "page_layout"), Query("SELECT COUNT(*) > 0 FROM catalog_pages WHERE id IN (5, 7) AND page_layout = 'vip_buy'")), Column("catalog_pages", "layout"), Table("club_membership_intervals")), Rerunnable: false),
        // 19_SecureLoginTokens: adds users.auth_ticket_* and credential_generation, and creates user_sessions, user_access_tokens and
        // user_remember_tokens (+ grace_uses) (file:5-70). 40 converts the times; the columns stay.
        // Rerun: line 15 clears every auth_ticket again, logging everyone out.
        new("Updates/19_SecureLoginTokens.sql", Column("user_remember_tokens", "grace_uses"), Rerunnable: false),
        // 21_CatalogAdminEditor: creates catalog_admin_log and furni_editor_log and seeds permissions/permissions_rights (file:5-60). 69
        // keeps both logs (69:2-3); 32 alters catalog_admin_log.
        // NOTE: optional user variable @catalog_admin_min_rank (defaults to 8).
        new("Updates/21_CatalogAdminEditor.sql", Table("furni_editor_log"), Rerunnable: false),
        // 20_Housekeeping: creates housekeeping_log and housekeeping_online_peaks and seeds permissions/rights (file:3-47). 36 converts the
        // log times; the tables stay.
        // Rerun: fails after Updates/22: permissions tables dropped.
        new("Updates/20_Housekeeping.sql", Table("housekeeping_online_peaks"), Rerunnable: false),
        // NOTE: not transactional.
        new("Updates/22_RoleBasedAccessControl.sql", Table("roles"), Rerunnable: false),
        // 23_HabboClubMembership: drops catalog_club_offers.type, extends user_club_memberships, creates the club_gift_*,
        // club_credit_spending, club_paydays and club_membership_intervals tables (last effect, file:51-55). 38 rebuilds that table's PK; it
        // stays.
        // Rerun: CREATE TABLE without IF NOT EXISTS, DROP COLUMN, plain INSERT into acl_permissions.
        new("Updates/23_HabboClubMembership.sql", Table("club_membership_intervals"), Rerunnable: false),
        // 24_UserSettingsAndDateTimes: creates users_settings and backfills it, drops the users setting columns, and converts users and
        // user_info times to DATETIME (file:4-76). 69 drops 4 users_settings columns; the table stays.
        // NOTE: sets and restores session time_zone through a user variable (self-contained).
        new("Updates/24_UserSettingsAndDateTimes.sql", Table("users_settings"), Rerunnable: false),
        // NOTE: uses DELIMITER // and CREATE PROCEDURE `migrate_binary_enums_to_booleans`; the runner must handle DELIMITER.
        // NOTE: in strict sql_mode (MariaDB default) it fails on stock 2017 data.
        new("Updates/25_BinaryEnumsToBooleans.sql", Query("SELECT COUNT(*) = 0 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND COLUMN_TYPE IN ('enum(''0'',''1'')', 'enum(''1'',''0'')')"), Rerunnable: true),
        // 26_AvatarEffectActivationDateTime: user_effects.activated_stamp double -> DATETIME (file:4-10); later files keep datetime.
        // NOTE: sets and restores session time_zone through a user variable.
        // Rerun: convert-and-drop: a rerun reads DATETIME as unix seconds.
        new("Updates/26_AvatarEffectActivationDateTime.sql", DataType("user_effects", "activated_stamp", "datetime"), Rerunnable: false),
        // 20_UseUtcPetCreationTime: bots_petdata.createstamp int -> DATETIME(6) (file:3-16); no later file touches it.
        new("20_UseUtcPetCreationTime.sql", DataType("bots_petdata", "createstamp", "datetime"), Rerunnable: false),
        // 21_UseUtcRoomPromotionTimes: room_promotions.timestamp_start/timestamp_expire double -> DATETIME(6), both in one ALTER
        // (file:3-25); no later file touches them.
        new("21_UseUtcRoomPromotionTimes.sql", DataType("room_promotions", "timestamp_start", "datetime"), Rerunnable: false),
        // 22_UseUtcRoomBanExpiry: room_bans.expire int -> DATETIME(6) (file:3-16); 72 only changes the id columns.
        new("22_UseUtcRoomBanExpiry.sql", DataType("room_bans", "expire", "datetime"), Rerunnable: false),
        // No later file changes the type of any DATETIME(6) column used below. 70_Utf8mb4InnoDB only converts charsets, and 71/72 only touch
        // keys and id columns.
        // 23_UseUtcRoomVisitTimes: user_roomvisits.entry_timestamp/exit_timestamp DOUBLE -> DATETIME(6) via add/convert/drop/rename
        // (file:3-29). Before: double. No later file changes the type.
        new("23_UseUtcRoomVisitTimes.sql", DataType("user_roomvisits", "entry_timestamp", "datetime"), Rerunnable: false),
        // 24_UseUtcChatlogTimes: chatlogs.timestamp DOUBLE -> DATETIME(6) (file:3-16). Before: double. No later file changes the type.
        new("24_UseUtcChatlogTimes.sql", DataType("chatlogs", "timestamp", "datetime"), Rerunnable: false),
        // 25_UseUtcMarketplaceTimes: catalog_marketplace_offers.timestamp DOUBLE -> new column listed_at DATETIME(6) (file:4-17). Before:
        // listed_at absent.
        new("25_UseUtcMarketplaceTimes.sql", DataType("catalog_marketplace_offers", "listed_at", "datetime"), Rerunnable: false),
        // 26_UseUtcGroupCreationTime: groups.created INT(50) -> DATETIME(6) (file:4-17). Before: int(50). No later file changes the type.
        new("26_UseUtcGroupCreationTime.sql", DataType("groups", "created", "datetime"), Rerunnable: false),
        // 27_UseUtcRewardTimes: server_rewards.reward_start/reward_end INT -> DATETIME(6) (file:1-23). Before: int(11).
        new("27_UseUtcRewardTimes.sql", DataType("server_rewards", "reward_start", "datetime"), Rerunnable: false),
        // 28_UseUtcQuestDefinitionTimes: quests.timestamp_unlock/timestamp_lock INT -> DATETIME(6) (file:4-26). Before: int(11).
        new("28_UseUtcQuestDefinitionTimes.sql", DataType("quests", "timestamp_unlock", "datetime"), Rerunnable: false),
        // 29_UseUtcAmbassadorLogTimes: ambassador_logs.timestamp DOUBLE -> DATETIME(6) (file:1-14). Before: double. 69 keeps the table.
        new("29_UseUtcAmbassadorLogTimes.sql", DataType("ambassador_logs", "timestamp", "datetime"), Rerunnable: false),
        // 33_UseUtcRoomInvitationTimes: chatlogs_console_invitations.timestamp DOUBLE -> DATETIME(6) (file:1-14). Before: double.
        new("33_UseUtcRoomInvitationTimes.sql", DataType("chatlogs_console_invitations", "timestamp", "datetime"), Rerunnable: false),
        // 30_UseUtcNameChangeLogTimes: logs_client_namechange.timestamp INT -> DATETIME(6) (file:4-17). Before: int(11).
        new("30_UseUtcNameChangeLogTimes.sql", DataType("logs_client_namechange", "timestamp", "datetime"), Rerunnable: false),
        // 32_RemoveBuildersClub: deletes BC pages/offers/tasks, drops catalog_pages.catalog_mode and narrows catalog_admin_log.catalog_type
        // enum('NORMAL','BUILDER') -> enum('NORMAL') (file:3-61). 89_BuildersClubCatalog re-adds catalog_mode, so it is not usable as a
        // marker.
        // No later file touches catalog_admin_log.catalog_type, and 69 keeps the table.
        // NOTE: never rerun on a database where 89-91 ran: the recursive delete (file:7-26) would wipe the re-added Builders Club catalogue.
        new("32_RemoveBuildersClub.sql", ColumnType("catalog_admin_log", "catalog_type", "enum('NORMAL')"), Rerunnable: false),
        // 31_UseUtcMessengerTimes: chatlogs_console.timestamp (file:5-18) and messenger_offline_messages.timestamp (file:20-31) DOUBLE ->
        // DATETIME(6).
        // Any() so a half-applied run (first table converted) is not converted twice. Before: both double.
        new("31_UseUtcMessengerTimes.sql", Any(DataType("chatlogs_console", "timestamp", "datetime"), DataType("messenger_offline_messages", "timestamp", "datetime")), Rerunnable: false),
        // 34_UseUtcModerationBanTimes: bans.expire DOUBLE and bans.added_date VARCHAR(50) -> DATETIME(6) (file:4-29). 69 only drops
        // bans.appeal_state.
        new("34_UseUtcModerationBanTimes.sql", Any(DataType("bans", "expire", "datetime"), DataType("bans", "added_date", "datetime")), Rerunnable: false),
        // 35_UseUtcRewardTrackTimes: reward_tracks.starts_at/ends_at (file:5-27) and users_reward_track_prizes.claimed_at (file:29-42) INT
        // -> DATETIME(6).
        new("35_UseUtcRewardTrackTimes.sql", Any(DataType("reward_tracks", "starts_at", "datetime"), DataType("users_reward_track_prizes", "claimed_at", "datetime")), Rerunnable: false),
        // 36_UseUtcAuditLogTimes: logs_client_staff.timestamp DOUBLE (file:5-18) and housekeeping_log.timestamp INT (file:20-35) ->
        // DATETIME(6).
        new("36_UseUtcAuditLogTimes.sql", Any(DataType("logs_client_staff", "timestamp", "datetime"), DataType("housekeeping_log", "timestamp", "datetime")), Rerunnable: false),
        // 37_UseUtcHabbiconUsageTimes: users_habbicons.last_used BIGINT epoch-ms -> DATETIME(6) (file:1-16). Before: bigint(20).
        new("37_UseUtcHabbiconUsageTimes.sql", DataType("users_habbicons", "last_used", "datetime"), Rerunnable: false),
        // 24_RoleChatCommands: INSERT IGNORE acl keys command.giverole/takerole and grants them to every role holding
        // housekeeping.roles.manage (file:2-9).
        // The acl keys are no marker: AccessControl.Start registers all command keys at every boot. Marker: one of the grants exists, or no
        // role holds housekeeping.roles.manage, in which case the file has nothing to grant. Before: the housekeeping role holds manage and
        // no grant exists.
        // A rerun re-grants roles the owner revoked on purpose.
        new("Updates/24_RoleChatCommands.sql", All(Table("role_permissions"), Any(Query("SELECT COUNT(*) > 0 FROM role_permissions WHERE permission_key IN ('command.giverole', 'command.takerole')"), Not(Query("SELECT COUNT(*) > 0 FROM role_permissions WHERE permission_key = 'housekeeping.roles.manage'")))), Rerunnable: false),
        // 41_UseUtcCatalogPromotionTimes: catalog_promotions.expires_at INT -> DATETIME(6) (file:3-14). Before: int(11). 69 drops only
        // unknown/parent_id.
        new("41_UseUtcCatalogPromotionTimes.sql", DataType("catalog_promotions", "expires_at", "datetime"), Rerunnable: false),
        // 42_UseUtcTradeAuditTimes: logs_client_trade.timestamp CHAR(20) -> DATETIME(6) (file:3-17). Before: char(20).
        new("42_UseUtcTradeAuditTimes.sql", DataType("logs_client_trade", "timestamp", "datetime"), Rerunnable: false),
        // 38_UseUtcClubTimes: BIGINT -> DATETIME(6) on user_club_memberships, club_membership_intervals (PK rebuilt), club_gift_claims,
        // club_credit_spending and club_paydays (PK rebuilt) (file:11-87). Before: all bigint(20). 52 renames only
        // club_gift_claims.catalog_item_id.
        // NOTE: preflight (file:4-8) aborts the file with a duplicate-key error when any interval start or payday is <= 0 or past 9999.
        // Nothing is altered then, so the probe stays false and the runner fails again until the operator fixes those rows.
        new("38_UseUtcClubTimes.sql", Any(DataType("user_club_memberships", "expires_at", "datetime"), DataType("club_membership_intervals", "started_at", "datetime"), DataType("club_gift_claims", "claimed_at", "datetime"), DataType("club_credit_spending", "spent_at", "datetime"), DataType("club_paydays", "payday", "datetime")), Rerunnable: false),
        // 39_UseUtcWiredVariableTimes: wired_variable_values.created_at_ms/updated_at_ms BIGINT -> created_at/updated_at DATETIME(6)
        // (file:1-23).
        // Before: created_at absent. 66 modifies only `value`.
        new("39_UseUtcWiredVariableTimes.sql", DataType("wired_variable_values", "created_at", "datetime"), Rerunnable: false),
        // 40_UseUtcCredentialTimes: users.auth_ticket_expires_at and the user_access_tokens/user_remember_tokens/user_sessions times
        // INT UNSIGNED -> DATETIME(6) (file:21-77). Before: int(11) unsigned. No later file alters these columns (88 adds a separate table).
        // NOTE: uses user variables + PREPARE/EXECUTE, and the preflight (file:3-19) SIGNALs 45000 when a tombstone is <= 0 or out of range.
        // The file then changes nothing and needs operator repair.
        new("40_UseUtcCredentialTimes.sql", Any(DataType("users", "auth_ticket_expires_at", "datetime"), DataType("user_access_tokens", "created_at", "datetime"), DataType("user_remember_tokens", "created_at", "datetime"), DataType("user_sessions", "created_at", "datetime")), Rerunnable: false),
        // 43_RestoreWiredCatalogOfferIds: data-only. Sets catalog_items.offer_id for 140 official wired (item_name, sprite_id) rows whose
        // offer_id <= 0 and whose official id is unclaimed (file:3-288). Probe = "the UPDATE has nothing left to change" (exact copy of its
        // join/WHERE), or catalog_items is gone (52 dropped it, file 52:358, which implies 43 ran; running 43 then would fail on the
        // missing table).
        // A rerun is idempotent while catalog_items exists. It is unsafe only because it errors after 52.
        // NOTE: data-dependent.
        new("43_RestoreWiredCatalogOfferIds.sql", Any(NoTable("catalog_items"), All(Table("furniture"), Not(Query("SELECT COUNT(*) > 0 FROM catalog_items AS item JOIN furniture AS furni ON item.item_id = CAST(furni.id AS CHAR) JOIN (SELECT 'wf_act_adjust_clock' AS item_name, 14555 AS sprite_id, 28809 AS offer_id UNION ALL SELECT 'wf_act_bot_clothes',7848,16064 UNION ALL SELECT 'wf_act_bot_follow_avatar',7851,16067 UNION ALL SELECT 'wf_act_bot_give_handitem',7852,16068 UNION ALL SELECT 'wf_act_bot_move',7853,16069 UNION ALL SELECT 'wf_act_bot_talk',7857,16073 UNION ALL SELECT 'wf_act_bot_talk_to_avatar',7855,16071 UNION ALL SELECT 'wf_act_bot_teleport',7849,16065 UNION ALL SELECT 'wf_act_call_stacks',4834,11865 UNION ALL SELECT 'wf_act_chase',5055,12519 UNION ALL SELECT 'wf_act_click_conf',18673,34242 UNION ALL SELECT 'wf_act_control_clock',13390,26586 UNION ALL SELECT 'wf_act_flee',5061,12525 UNION ALL SELECT 'wf_act_freeze',14126,28193 UNION ALL SELECT 'wf_act_furni_to_furni',14328,28415 UNION ALL SELECT 'wf_act_furni_to_user',14340,28427 UNION ALL SELECT 'wf_act_give_score',3697,9132 UNION ALL SELECT 'wf_act_give_score_tm',5043,12507 UNION ALL SELECT 'wf_act_join_team',5062,12526 UNION ALL SELECT 'wf_act_kick_user',4947,12303 UNION ALL SELECT 'wf_act_leave_team',5049,12513 UNION ALL SELECT 'wf_act_log',17599,32843 UNION ALL SELECT 'wf_act_match_to_sshot',3700,9135 UNION ALL SELECT 'wf_act_move_furni_as_group',18783,34390 UNION ALL SELECT 'wf_act_move_rotate',3663,9098 UNION ALL SELECT 'wf_act_move_to_dir',5048,12512 UNION ALL SELECT 'wf_act_neg_call_stacks',14557,28811 UNION ALL SELECT 'wf_act_neg_log',17598,32842 UNION ALL SELECT 'wf_act_neg_send_signal',14556,28810 UNION ALL SELECT 'wf_act_place_furni',18784,34391 UNION ALL SELECT 'wf_act_rel_mov',14127,28194 UNION ALL SELECT 'wf_act_remove_furni',18785,34392 UNION ALL SELECT 'wf_act_reset_timers',3691,9126 UNION ALL SELECT 'wf_act_send_signal',14085,28103 UNION ALL SELECT 'wf_act_set_altitude',14092,28110 UNION ALL SELECT 'wf_act_show_message',3681,9116 UNION ALL SELECT 'wf_act_teleport_to',3674,9109 UNION ALL SELECT 'wf_act_teleport_to_room',16743,31666 UNION ALL SELECT 'wf_act_toggle_state',3685,9120 UNION ALL SELECT 'wf_act_unfreeze',14130,28197 UNION ALL SELECT 'wf_cnd_actor_dir',14129,28196 UNION ALL SELECT 'wf_cnd_actor_in_group',4281,10597 UNION ALL SELECT 'wf_cnd_actor_in_team',5056,12520 UNION ALL SELECT 'wf_cnd_counter_time_matches',13810,27637 UNION ALL SELECT 'wf_cnd_date_rng_active',5861,13753 UNION ALL SELECT 'wf_cnd_furnis_hv_avtrs',3692,9127 UNION ALL SELECT 'wf_cnd_has_altitude',14093,28111 UNION ALL SELECT 'wf_cnd_has_furni_on',3857,9558 UNION ALL SELECT 'wf_cnd_has_handitem',7854,16070 UNION ALL SELECT 'wf_cnd_match_date',13803,27630 UNION ALL SELECT 'wf_cnd_match_snapshot',3695,9130 UNION ALL SELECT 'wf_cnd_match_time',13804,27631 UNION ALL SELECT 'wf_cnd_not_furni_on',5440,13121 UNION ALL SELECT 'wf_cnd_not_has_handitem',13806,27633 UNION ALL SELECT 'wf_cnd_not_hv_avtrs',5441,13122 UNION ALL SELECT 'wf_cnd_not_in_group',5448,13129 UNION ALL SELECT 'wf_cnd_not_in_team',5439,13120 UNION ALL SELECT 'wf_cnd_not_match_snap',5452,13133 UNION ALL SELECT 'wf_cnd_not_stuff_is',5449,13130 UNION ALL SELECT 'wf_cnd_not_trggrer_on',5438,13119 UNION ALL SELECT 'wf_cnd_not_triggerer_match',13808,27635 UNION ALL SELECT 'wf_cnd_not_user_count',5443,13124 UNION ALL SELECT 'wf_cnd_not_user_performs_action',13802,27629 UNION ALL SELECT 'wf_cnd_slc_quantity',14335,28422 UNION ALL SELECT 'wf_cnd_stuff_is',5447,13128 UNION ALL SELECT 'wf_cnd_team_has_rank',13811,27638 UNION ALL SELECT 'wf_cnd_team_has_score',13812,27639 UNION ALL SELECT 'wf_cnd_time_less_than',3682,9117 UNION ALL SELECT 'wf_cnd_time_more_than',3665,9100 UNION ALL SELECT 'wf_cnd_trggrer_on_frn',3694,9129 UNION ALL SELECT 'wf_cnd_triggerer_match',13805,27632 UNION ALL SELECT 'wf_cnd_user_count_in',5445,13126 UNION ALL SELECT 'wf_cnd_user_performs_action',13809,27636 UNION ALL SELECT 'wf_cnd_valid_moves',14553,28807 UNION ALL SELECT 'wf_slc_furni_altitude',14330,28417 UNION ALL SELECT 'wf_slc_furni_area',14333,28420 UNION ALL SELECT 'wf_slc_furni_bytype',14334,28421 UNION ALL SELECT 'wf_slc_furni_neighborhood',14342,28429 UNION ALL SELECT 'wf_slc_furni_onfurni',14329,28416 UNION ALL SELECT 'wf_slc_furni_picks',14337,28424 UNION ALL SELECT 'wf_slc_furni_signal',14326,28413 UNION ALL SELECT 'wf_slc_users_area',14325,28412 UNION ALL SELECT 'wf_slc_users_byaction',14339,28426 UNION ALL SELECT 'wf_slc_users_byname',14327,28414 UNION ALL SELECT 'wf_slc_users_bytype',14343,28430 UNION ALL SELECT 'wf_slc_users_group',14331,28418 UNION ALL SELECT 'wf_slc_users_handitem',14336,28423 UNION ALL SELECT 'wf_slc_users_neighborhood',14346,28433 UNION ALL SELECT 'wf_slc_users_onfurni',14341,28428 UNION ALL SELECT 'wf_slc_users_signal',14338,28425 UNION ALL SELECT 'wf_slc_users_team',14345,28432 UNION ALL SELECT 'wf_trg_at_given_time',3679,9114 UNION ALL SELECT 'wf_trg_bot_reached_avtr',7856,16072 UNION ALL SELECT 'wf_trg_bot_reached_stf',7850,16066 UNION ALL SELECT 'wf_trg_click_furni',14089,28107 UNION ALL SELECT 'wf_trg_click_tile',14554,28808 UNION ALL SELECT 'wf_trg_click_user',16878,31820 UNION ALL SELECT 'wf_trg_clock_counter',13385,26581 UNION ALL SELECT 'wf_trg_collision',5050,12514 UNION ALL SELECT 'wf_trg_enter_room',3683,9118 UNION ALL SELECT 'wf_trg_game_ends',3680,9115 UNION ALL SELECT 'wf_trg_game_starts',3702,9137 UNION ALL SELECT 'wf_trg_period_long',5042,12506 UNION ALL SELECT 'wf_trg_period_short',14128,28195 UNION ALL SELECT 'wf_trg_periodically',3671,9106 UNION ALL SELECT 'wf_trg_recv_signal',14090,28108 UNION ALL SELECT 'wf_trg_says_something',3675,9110 UNION ALL SELECT 'wf_trg_score_achieved',3673,9108 UNION ALL SELECT 'wf_trg_state_changed',3668,9103 UNION ALL SELECT 'wf_trg_stuff_state',14091,28109 UNION ALL SELECT 'wf_trg_user_performs_action',13807,27634 UNION ALL SELECT 'wf_trg_walks_off_furni',3678,9113 UNION ALL SELECT 'wf_trg_walks_on_furni',3703,9138 UNION ALL SELECT 'wf_var_echo',16910,31921 UNION ALL SELECT 'wf_xtra_anim_time',14131,28198 UNION ALL SELECT 'wf_xtra_execution_limit',14098,28116 UNION ALL SELECT 'wf_xtra_filter_furni',14344,28431 UNION ALL SELECT 'wf_xtra_filter_users',14332,28419 UNION ALL SELECT 'wf_xtra_mov_carry_users',14096,28114 UNION ALL SELECT 'wf_xtra_mov_curve',18674,34243 UNION ALL SELECT 'wf_xtra_mov_no_animation',14088,28106 UNION ALL SELECT 'wf_xtra_mov_physics',14086,28104 UNION ALL SELECT 'wf_xtra_random',3669,9104 UNION ALL SELECT 'wf_xtra_rotate_to_dir',18675,34244 UNION ALL SELECT 'wf_xtra_text_output_furni_name',18495,33989 UNION ALL SELECT 'wf_xtra_unseen',3670,9105 UNION ALL SELECT 'wf_xtra_var_fx_boss',19195,34995 UNION ALL SELECT 'wf_xtra_var_fx_health',19196,34996 UNION ALL SELECT 'wf_xtra_var_fx_level',19197,34997 UNION ALL SELECT 'wf_xtra_var_fx_number',19198,34998 UNION ALL SELECT 'wf_xtra_var_fx_progress',19199,34999 UNION ALL SELECT 'wf_xtra_var_fx_status',19200,35000 UNION ALL SELECT 'wf_xtra_var_lvlup_system',16909,31920 UNION ALL SELECT 'wf_xtra_var_time_util',16908,31919 UNION ALL SELECT 'wf_upcounter1',13383,26579 UNION ALL SELECT 'wf_upcounter2',13396,26592 UNION ALL SELECT 'wf_game_upcounter1',13395,26591 UNION ALL SELECT 'wf_game_upcounter2',13384,26580 UNION ALL SELECT 'wf_antenna1',14097,28115 UNION ALL SELECT 'wf_antenna2',14095,28113) AS official ON official.item_name = furni.item_name AND official.sprite_id = furni.sprite_id LEFT JOIN catalog_items AS claimed ON claimed.offer_id = official.offer_id AND claimed.item_id <> item.item_id WHERE item.offer_id <= 0 AND claimed.id IS NULL")))), Rerunnable: false),
        // 44_WidenOnlineTime: user_statistics.OnlineTime INT(7) -> BIGINT (file:1). Before: int(7). Rerunning it repeats an identical
        // MODIFY.
        new("44_WidenOnlineTime.sql", DataType("user_statistics", "OnlineTime", "bigint"), Rerunnable: true),
        // 57_SnowStorm: CREATE TABLE IF NOT EXISTS snowwar_scores/_game_tokens/_token_offers plus INSERT IGNORE offers and gamecenter.*
        // settings (file:6-57).
        // The settings insert names `description`, which 69 drops, so a rerun after 69 errors, and before that it re-seeds deleted
        // offers/settings.
        new("Updates/57_SnowStorm.sql", Table("snowwar_scores"), Rerunnable: false),
        // 48_MarketplaceListingPolicy: INSERT IGNORE server_settings 'catalog.marketplace.only_rare_ltd' (file:1-2). It names `description`,
        // which 69 drops (69:24), so a run after 69 errors. NoColumn(description) counts as applied. A rerun before 69 re-seeds a row the owner
        // deleted.
        new("48_MarketplaceListingPolicy.sql", All(Table("server_settings"), Any(Query("SELECT COUNT(*) > 0 FROM server_settings WHERE `key` = 'catalog.marketplace.only_rare_ltd'"), NoColumn("server_settings", "description"))), Rerunnable: false),
        // 27_BadgeRarityScale: INSERT IGNORE of 12 badge.rarity.* server_settings rows (file:4-16). It names the `description` column, which
        // 69_DropUnusedSchema drops (69:24), so a run after 69 errors. NoColumn(description) therefore counts as applied (69 is later in
        // order).
        new("Updates/27_BadgeRarityScale.sql", All(Table("server_settings"), Any(Query("SELECT COUNT(*) > 0 FROM server_settings WHERE `key` = 'badge.rarity.active_days'"), NoColumn("server_settings", "description"))), Rerunnable: false),
        // 45_Crafting: CREATE TABLE IF NOT EXISTS crafting_recipes/_altars_recipes/_recipes_ingredients/user_crafting_recipes, then seeds 24
        // recipes (file:1-157).
        // Before: crafting_recipes absent (also absent from the 2017 base). No later file drops it. A rerun re-seeds recipes the owner
        // deleted.
        // NOTE: has its own START TRANSACTION/COMMIT (file:146-156) and uses temporary tables.
        new("45_Crafting.sql", Table("crafting_recipes"), Rerunnable: false),
        // 47_TalentTrackRewards: guarded ADD of talents_sub_levels.talent_type plus CREATE TABLE IF NOT EXISTS user_talent_rewards
        // (file:1-16).
        // Marker: user_talent_rewards. talent_type already exists in some bases, so it is not usable. 69 drops only achievements_talents.
        // NOTE: uses a user variable + PREPARE/EXECUTE (file:1-9).
        new("47_TalentTrackRewards.sql", Table("user_talent_rewards"), Rerunnable: true),
        // 46_RoomPolls: CREATE TABLE IF NOT EXISTS room_polls/room_poll_questions/room_poll_responses (file:1-31). 72 modifies only
        // room_polls.room_id.
        new("46_RoomPolls.sql", Table("room_polls"), Rerunnable: true),
        // 50_RoomWordQuizCommand: INSERT IGNORE acl key command.wordquiz and grants it to roles.slug='default' (file:1-5). The acl key is
        // registered
        // by the emulator at boot, so the marker is the grant (or no 'default' role, so nothing to grant). A rerun re-grants a revoked
        // permission.
        new("50_RoomWordQuizCommand.sql", All(Table("role_permissions"), Table("roles"), Any(Query("SELECT COUNT(*) > 0 FROM role_permissions WHERE permission_key = 'command.wordquiz'"), Not(Query("SELECT COUNT(*) > 0 FROM roles WHERE slug = 'default'")))), Rerunnable: false),
        // 49_IceTagPole: guarded UPDATE furniture es_tagging/3741 interaction 'default' -> 'icetag_pole' (file:2-3). Probe = no row left to
        // fix.
        // Before: the official row is 'default'. Later files never set it back (91 only lists es_tagging as a BC offer).
        // NOTE: data-dependent.
        new("49_IceTagPole.sql", All(Table("furniture"), Not(Query("SELECT COUNT(*) > 0 FROM furniture WHERE item_name = 'es_tagging' AND sprite_id = 3741 AND interaction_type = 'default'"))), Rerunnable: true),
        // 51_Recycler: CREATE TABLE IF NOT EXISTS recycler_settings/levels/prizes/user_recycler plus INSERT IGNORE settings row id 1
        // (file:2-23).
        // Before: recycler_settings absent. The only rerun effect is re-seeding a deleted settings row 1.
        new("51_Recycler.sql", Table("recycler_settings"), Rerunnable: false),
        // 56_SafetyQuizzes: CREATE TABLE IF NOT EXISTS safety_quizzes/safety_quiz_questions/user_safety_quizzes plus guarded seeds of
        // SafetyQuiz1/HabboWay1,
        // two achievements and two badges (file:2-92). A rerun re-seeds quizzes, achievements and badges the owner deleted.
        // NOTE: own START TRANSACTION/COMMIT (file:24-92) and user variables (@seed_safety, @seed_habbo_way).
        new("56_SafetyQuizzes.sql", Table("safety_quizzes"), Rerunnable: false),
        // 53_CampaignCalendar: CREATE TABLE IF NOT EXISTS campaign_calendars/campaign_calendar_rewards/user_calendar_claims (file:1-34).
        new("Updates/53_CampaignCalendar.sql", Table("campaign_calendars"), Rerunnable: true),
        // 54_GroupForums: CREATE TABLE IF NOT EXISTS group_forums/_threads/_messages/_read_markers/_post_limits (file:1-58). Not in the 2017
        // base.
        new("54_GroupForums.sql", Table("group_forums"), Rerunnable: true),
        // 52_NormalizeCatalog: creates catalog_offers/_offer_products/_offer_limited/_page_offers/_page_images/_page_texts, rebuilds
        // catalog_pages and
        // club_gift_offers, then drops catalog_items and catalog_deals (file:12-359). End state: catalog_offers exists and catalog_items
        // does not.
        // No later file recreates catalog_items, and 87/89-92 only insert into catalog_offers.
        // NOTE: no transaction.
        new("52_NormalizeCatalog.sql", All(Table("catalog_offers"), NoTable("catalog_items")), Rerunnable: false),
        // 55_RoomMusic: CREATE TABLE IF NOT EXISTS room_music_songs/_disc_definitions/_players/_playlist (file:1-33).
        new("Updates/55_RoomMusic.sql", Table("room_music_songs"), Rerunnable: true),
        // 58_SnowStormRayGuns: INSERT IGNORE of server_settings 'gamecenter.snowwar.raygun.enabled' (file:3-4); no later file deletes it
        // (69 only drops server_settings.description, which the row survives).
        // rerun: a rerun after 69 errors: its INSERT names server_settings.description, which 69 drops (verified).
        new("Updates/58_SnowStormRayGuns.sql", All(Table("server_settings"), Query("SELECT COUNT(*) > 0 FROM server_settings WHERE `key` = 'gamecenter.snowwar.raygun.enabled'")), Rerunnable: false),
        // 59_UserCurrencies: creates user_currencies, copies the three balances, drops users.activity_points/vip_points/gotw_points
        // (file:3-24);
        // later files only convert/key the table (70, 71) and nothing re-adds the users columns.
        // NOTE: has its own START TRANSACTION/COMMIT (file:12-22).
        // rerun: a rerun errors (users.activity_points is gone); before the drop it would overwrite user_currencies amounts.
        new("59_UserCurrencies.sql", All(Table("user_currencies"), NoColumn("users", "activity_points")), Rerunnable: false),
        // 60_FurnitureFurnidata: adds furniture.has_furnidata and the other furnidata columns (file:15-45), then fills them from @furnidata;
        // no later file drops the columns (79/81/82/87/91/92 read them).
        // NOTE: needs the user variable @furnidata (base64-decoded FurnitureData.json) to fill the columns; without it only the columns are
        // added (file:9-13).
        // rerun: with @furnidata a rerun overwrites every furnidata column from the JSON (reverting 79/81/82/87 names) and re-creates
        // deleted furniture rows.
        new("60_FurnitureFurnidata.sql", Column("furniture", "has_furnidata"), Rerunnable: false),
        // 61_RconGrants: creates rcon_grants (file:4-13); 70/72 only convert it / add fk_rcon_grants_user_id.
        // rerun: CREATE TABLE IF NOT EXISTS only.
        new("61_RconGrants.sql", Table("rcon_grants"), Rerunnable: true),
        // 62_BuildHeightCommand: INSERT IGNORE acl_permissions 'command.bh' + default role grant (file:1-5); nothing later removes it.
        // rerun: re-grants command.bh to the default role if an admin revoked it.
        new("62_BuildHeightCommand.sql", All(Table("acl_permissions"), Query("SELECT COUNT(*) > 0 FROM acl_permissions WHERE `key` = 'command.bh'")), Rerunnable: false),
        // 63_HideWiredCommand: adds rooms.hide_wired (file:1) and the 'command.hidewired' permission; no later file touches the column.
        // rerun: re-grants command.hidewired to the default role if an admin revoked it (the column part is IF NOT EXISTS).
        new("63_HideWiredCommand.sql", Column("rooms", "hide_wired"), Rerunnable: false),
        // 73_RepairCameraPictureData: rewrites items.extra_data of bought photos whose data is not JSON (file:3-15). Pure data repair,
        // idempotent,
        // leaves no marker: the probe is its post-condition (photos exist and none is broken). False on hotels without photos (rerun is a
        // no-op).
        // NOTE: post-condition probe; true on a hotel that never ran 73 but has no broken photo (73 would change nothing there).
        // rerun: touches only photo items whose extra_data is not JSON; rerun verified to change nothing.
        new("73_RepairCameraPictureData.sql", All(Table("camera_purchases"), Table("camera_media"), Query("SELECT EXISTS (SELECT 1 FROM camera_purchases p JOIN items i ON i.id = p.item_id) AND NOT EXISTS (SELECT 1 FROM items i JOIN camera_purchases p ON p.item_id = i.id JOIN camera_media m ON m.id = p.media_id JOIN users u ON u.id = p.user_id WHERE i.extra_data NOT LIKE '{%')")), Rerunnable: true),
        // 69_DropUnusedSchema: drops six tables and a list of columns (file:7-34), all IF EXISTS; nothing later re-creates them.
        // Every pre-69 reference state has all four of these. The file is idempotent, so a false negative only reruns DROP IF EXISTS.
        // rerun: DROP ... IF EXISTS only.
        new("69_DropUnusedSchema.sql", All(NoTable("catalog_clothing"), NoTable("server_status"), NoColumn("furniture", "clothing_id"), NoColumn("users", "machine_id")), Rerunnable: true),
        // 70_Utf8mb4InnoDB: converts every table to InnoDB + utf8mb4_unicode_ci (file:8-277); users/furniture/rooms are latin1_swedish_ci before it.
        // rerun: each table is converted only while it is not InnoDB/utf8mb4; rerun on a converted database verified to change nothing (it
        // would convert tables created later in another collation).
        new("70_Utf8mb4InnoDB.sql", All(TableCollation("users", "utf8mb4_unicode_ci"), TableCollation("furniture", "utf8mb4_unicode_ci"), TableCollation("rooms", "utf8mb4_unicode_ci")), Rerunnable: true),
        // 71_KeysAndIndexes: room_rights loses id and gets PRIMARY KEY (room_id, user_id) (file:30); group_memberships gets UNIQUE
        // group_user (file:45).
        // 72 only adds FKs on these tables. NOT idempotent (DROP COLUMN id / DROP PRIMARY KEY fail on a rerun).
        // rerun: a rerun errors (DROP PRIMARY KEY / DROP COLUMN id on tables that no longer have them).
        new("71_KeysAndIndexes.sql", Any(PrimaryKey("room_rights", "room_id", "user_id"), Index("group_memberships", "group_user")), Rerunnable: false),
        // 72_ForeignKeys: adds rooms.fk_rooms_owner (file:706-708) and the generated items.room_ref (file:503-505). fk_items_user_id is not
        // used:
        // 65 drops it (FreshInstall builds have no fk_items_user_id). NOT idempotent (ADD COLUMN room_ref / ADD CONSTRAINT fail on a rerun).
        // NOTE: legacy order runs 65 before 72, so 72 re-adds fk_items_user_id (file:604-606) that 65 dropped for chest stock (items.user_id
        // = 0);
        // rerun: a rerun errors (ADD COLUMN room_ref / ADD CONSTRAINT already exist).
        new("72_ForeignKeys.sql", Any(ForeignKey("rooms", "fk_rooms_owner"), Column("items", "room_ref")), Rerunnable: false),
        // 64_WiredFurnitureInteractions: sets nine furniture.interaction_type values (file:3-37); 79 sets the same values again, nothing
        // reverts them.
        // Data-only and idempotent; false where none of the nine rows exist (rerun is a no-op).
        // rerun: UPDATEs guarded by the old interaction values; rerun verified to change nothing.
        new("64_WiredFurnitureInteractions.sql", Query("SELECT EXISTS (SELECT 1 FROM furniture WHERE type = 's' AND ((item_name = 'wf_xtra_varfx_hp' AND sprite_id = 19196 AND interaction_type = 'wf_xtra_var_fx_health') OR (item_name = 'wf_xtra_varfx_prog' AND sprite_id = 19199 AND interaction_type = 'wf_xtra_var_fx_progress') OR (item_name = 'wf_xtra_varfx_levelling' AND sprite_id = 19197 AND interaction_type = 'wf_xtra_var_fx_level') OR (item_name = 'wf_xtra_varfx_status' AND sprite_id = 19200 AND interaction_type = 'wf_xtra_var_fx_status') OR (item_name = 'wf_xtra_varfx_boss' AND sprite_id = 19195 AND interaction_type = 'wf_xtra_var_fx_boss') OR (item_name = 'wf_xtra_varfx_number' AND sprite_id = 19198 AND interaction_type = 'wf_xtra_var_fx_number') OR (item_name = 'wf_proto_trg_at_given_time' AND sprite_id = 17678 AND interaction_type = 'wf_trg_at_given_time') OR (item_name = 'wf_proto_cnd_trggrer_on_frn' AND sprite_id = 17679 AND interaction_type = 'wf_cnd_trggrer_on_frn') OR (item_name = 'wf_ltdproto_act_toggle_state' AND sprite_id = 17676 AND interaction_type = 'wf_act_toggle_state'))) AND NOT EXISTS (SELECT 1 FROM furniture WHERE type = 's' AND ((item_name = 'wf_xtra_varfx_hp' AND sprite_id = 19196 AND interaction_type IN ('default', 'wired_addon', 'wf_xtra_varfx_hp')) OR (item_name = 'wf_xtra_varfx_prog' AND sprite_id = 19199 AND interaction_type IN ('default', 'wired_addon', 'wf_xtra_varfx_prog')) OR (item_name = 'wf_xtra_varfx_levelling' AND sprite_id = 19197 AND interaction_type IN ('default', 'wired_addon', 'wf_xtra_varfx_levelling')) OR (item_name = 'wf_xtra_varfx_status' AND sprite_id = 19200 AND interaction_type IN ('default', 'wired_addon', 'wf_xtra_varfx_status')) OR (item_name = 'wf_xtra_varfx_boss' AND sprite_id = 19195 AND interaction_type IN ('default', 'wired_addon', 'wf_xtra_varfx_boss')) OR (item_name = 'wf_xtra_varfx_number' AND sprite_id = 19198 AND interaction_type IN ('default', 'wired_addon', 'wf_xtra_varfx_number')) OR (item_name = 'wf_proto_trg_at_given_time' AND sprite_id = 17678 AND interaction_type IN ('default', 'wired_trigger', 'wf_proto_trg_at_given_time')) OR (item_name = 'wf_proto_cnd_trggrer_on_frn' AND sprite_id = 17679 AND interaction_type IN ('default', 'wired_condition', 'wf_proto_cnd_trggrer_on_frn')) OR (item_name = 'wf_ltdproto_act_toggle_state' AND sprite_id = 17676 AND interaction_type IN ('default', 'wired_effect', 'wf_ltdproto_act_toggle_state'))))"), Rerunnable: true),
        // 65_WiredChests: creates wired_chests/wired_contracts/wired_chest_transactions(_entries), items.chest_* and items.user_ref
        // (file:2-60);
        // no later file drops them. (Its own prepared statements need no runner support.)
        // rerun: a rerun drops fk_items_user_id (re-added by 72 in legacy order; verified) and re-applies unguarded interaction_type
        // UPDATEs.
        new("65_WiredChests.sql", All(Table("wired_chests"), Column("items", "chest_item_id")), Rerunnable: false),
        // 66_WiredVariableValueWidth: wired_variable_values.value INT -> BIGINT (file:1); 70/71/72 do not modify that column.
        // rerun: MODIFY to the same BIGINT; rerun verified to change nothing.
        new("66_WiredVariableValueWidth.sql", DataType("wired_variable_values", "value", "bigint"), Rerunnable: true),
        // 67_AreaHideInteraction: conf_area_hide (sprite 15215) interaction -> 'area_hide' (file:1-3); nothing later changes it. Data-only,
        // idempotent.
        // rerun: UPDATE guarded by the old interaction values.
        new("67_AreaHideInteraction.sql", Query("SELECT EXISTS (SELECT 1 FROM furniture WHERE item_name = 'conf_area_hide' AND type = 's' AND sprite_id = 15215 AND interaction_type = 'area_hide') AND NOT EXISTS (SELECT 1 FROM furniture WHERE item_name = 'conf_area_hide' AND type = 's' AND sprite_id = 15215 AND interaction_type IN ('default', 'default_floor', 'conf_area_hide'))"), Rerunnable: true),
        // 68_RoomModelPresentation: adds room_models.half_scale/camera_x/camera_y/camera_z (file:1-5); 69 drops only room_models.poolmap.
        // rerun: ADD COLUMN IF NOT EXISTS only.
        new("68_RoomModelPresentation.sql", Column("room_models", "half_scale"), Rerunnable: true),
        // 74_RepairAdjustableFurnitureHeights: bc_block_1*14 / bc_stick*60 height_adjustable fixed (file:1-7); nothing later touches them.
        // rerun: UPDATEs guarded by the old values.
        new("74_RepairAdjustableFurnitureHeights.sql", Query("SELECT EXISTS (SELECT 1 FROM furniture WHERE type = 's' AND ((item_name = 'bc_block_1*14' AND sprite_id = 5481 AND height_adjustable = '1.0,0.75,0.5,0.25,0.0') OR (item_name = 'bc_stick*60' AND sprite_id = 12761 AND height_adjustable = '4.0,3.0,2.0,1.0,0.0'))) AND NOT EXISTS (SELECT 1 FROM furniture WHERE type = 's' AND ((item_name = 'bc_block_1*14' AND sprite_id = 5481 AND height_adjustable = '1.0,0.75,0.5,0.25,0.1') OR (item_name = 'bc_stick*60' AND sprite_id = 12761 AND height_adjustable = '4.4,3.3,2.2,1.1,0.0')))"), Rerunnable: true),
        // 75_DiagonalSqueeze: adds rooms.diagonal_squeeze (file:3); no later file touches it.
        // rerun: ADD COLUMN IF NOT EXISTS only.
        new("75_DiagonalSqueeze.sql", Column("rooms", "diagonal_squeeze"), Rerunnable: true),
        // 76_OfficialAchievements: adds achievements.official_id/state/visible/badge_code + UNIQUE group_level, seeds the official set
        // (file:14-19);
        // it converts user progress only on the run that adds official_id (file:11-12), so a rerun is safe. Nothing later drops the column.
        // rerun: a rerun re-upserts the official set (visible = 1, category, thresholds), hides non-official rows again (undoing 84 for
        // ACH_Motto) and deletes extra levels; verified to change achievements.
        new("76_OfficialAchievements.sql", Column("achievements", "official_id"), Rerunnable: false),
        // 77_AchievementBadgeCleanup: deletes every user_badges row below the highest level held per achievement (file:6-55). Data-only,
        // idempotent, no marker: post-condition (badges exist and no user holds two levels of one achievement). Guarded by 76's badge_code.
        // NOTE: post-condition probe; true on a hotel that never ran 77 but has no duplicate level badges (77 would change nothing there).
        // rerun: removes only lower-level duplicates; rerun verified to change nothing.
        new("77_AchievementBadgeCleanup.sql", All(Column("achievements", "badge_code"), Table("user_badges"), Query("SELECT EXISTS (SELECT 1 FROM user_badges) AND NOT EXISTS (SELECT 1 FROM achievements a JOIN user_badges ub ON ub.badge_id = COALESCE(a.badge_code, CONCAT(a.group_name, a.level)) GROUP BY ub.user_id, a.group_name HAVING COUNT(DISTINCT a.level) > 1)")), Rerunnable: true),
        // 78_InvisibleFurniControlInteraction: conf_invis_control interaction -> 'invis_control' (file:1-3); nothing later changes it.
        // Data-only, idempotent.
        // (v2026.10.15-7704996's FreshInstall predates it: false there, as it should be.)
        // rerun: UPDATE guarded by the old interaction values.
        new("78_InvisibleFurniControlInteraction.sql", Query("SELECT EXISTS (SELECT 1 FROM furniture WHERE item_name = 'conf_invis_control' AND type = 's' AND interaction_type = 'invis_control') AND NOT EXISTS (SELECT 1 FROM furniture WHERE item_name = 'conf_invis_control' AND type = 's' AND interaction_type IN ('default', 'default_floor', 'conf_invis_control'))"), Rerunnable: true),
        // 79_WiredFurnitureAndCatalogue: fixes Wired interactions, the click tile interaction (file:148-149), three placeholder names
        // (file:152-157)
        // and adds offers (82 later re-sorts the Wired pages, so offers are not used). Idempotent. Marker: click tile / renamed boxes carry
        // the new
        // values and none still has the old one. Needs 60's furniture.name.
        // NOTE: has its own START TRANSACTION/COMMIT (file:8, 219).
        // rerun: a rerun re-adds offers that 82 moved off the Wired pages (verified: catalog_offers/products/page_offers change).
        new("79_WiredFurnitureAndCatalogue.sql", All(Column("furniture", "name"), Query("SELECT EXISTS (SELECT 1 FROM furniture WHERE type = 's' AND ((item_name = 'room_invisible_click_tile' AND sprite_id = 14561 AND interaction_type = 'room_invisible_click_tile') OR (item_name = 'wf_trg_at_time_long' AND name = 'WIRED Trigger: At Set Time (Long)') OR (item_name = 'wf_cnd_not_wearing_b' AND name = 'WIRED Negative Condition: User Is NOT Wearing Badge') OR (item_name = 'wf_cnd_not_wearing_fx' AND name = 'WIRED Negative Condition: User Is NOT Wearing Effect'))) AND NOT EXISTS (SELECT 1 FROM furniture WHERE (item_name = 'room_invisible_click_tile' AND type = 's' AND sprite_id = 14561 AND interaction_type <> 'room_invisible_click_tile') OR (item_name IN ('wf_trg_at_time_long', 'wf_cnd_not_wearing_b', 'wf_cnd_not_wearing_fx') AND name = CONCAT(item_name, ' name')))")), Rerunnable: false),
        // 80_WiredRoomLinker: wf_room_linker interaction -> 'wired_room_linker' (file:4-6); nothing later changes it. Data-only, idempotent.
        // rerun: UPDATE guarded by the old interaction values.
        new("80_WiredRoomLinker.sql", Query("SELECT EXISTS (SELECT 1 FROM furniture WHERE item_name = 'wf_room_linker' AND type = 's' AND interaction_type = 'wired_room_linker') AND NOT EXISTS (SELECT 1 FROM furniture WHERE item_name = 'wf_room_linker' AND type = 's' AND interaction_type IN ('default', 'default_floor', 'wf_room_linker'))"), Rerunnable: true),
        // 81_WiredOctoberBoxes: creates wired_labels, wired_link_networks, wired_link_endpoints, wired_web_api_keys (file:8-48) +
        // furni/offers;
        // no later file touches these tables.
        // NOTE: has its own START TRANSACTION/COMMIT (file:50, 169), after its DDL.
        // rerun: re-creates deleted furniture rows and offers (verified: catalog_offers/products/page_offers change).
        new("81_WiredOctoberBoxes.sql", All(Table("wired_labels"), Table("wired_web_api_keys")), Rerunnable: false),
        // 82_WiredCatalogueParity: inserts conf_chooser_disable with interaction 'chooser_disable' (file:18-31) and the 'plus_wired_more'
        // page
        // (file:37-39, only when the 'wired2' page exists). 91 only adds a BUILDERS_CLUB copy of conf_chooser_disable offers. Idempotent.
        // NOTE: has its own START TRANSACTION/COMMIT (file:15, 423).
        // rerun: catalogue tree/page-offer replace: reverts admin catalogue edits (verified: catalog_pages/page_offers change).
        new("82_WiredCatalogueParity.sql", Any(All(Column("catalog_pages", "link"), Query("SELECT COUNT(*) > 0 FROM catalog_pages WHERE link = 'plus_wired_more'")), Query("SELECT COUNT(*) > 0 FROM furniture WHERE item_name = 'conf_chooser_disable' AND type = 's' AND interaction_type = 'chooser_disable'")), Rerunnable: false),
        // 83_WiredArchiveText: sets slot 0 text of the 'wired_archive' page (file:4-6). 91 creates a BUILDERS_CLUB 'wired_archive' page (id
        // 2129322)
        // with the same text, so that page is excluded. Idempotent.
        // rerun: unguarded overwrite of the wired_archive page text (reverts an admin edit; no change otherwise).
        new("83_WiredArchiveText.sql", All(Table("catalog_page_texts"), Column("catalog_pages", "link"), Query("SELECT COUNT(*) > 0 FROM catalog_page_texts t JOIN catalog_pages c ON c.id = t.page_id WHERE c.link = 'wired_archive' AND c.id <> 2129322 AND t.slot = 0 AND t.text = 'Archived Wired boxes that are still available for legacy reasons, but are not recommended to use in new Wired setups'")), Rerunnable: false),
        // 84_MottoAchievementVisible: achievements ACH_Motto visible = 1 (file:5); 76 had set it to 0 and nothing later changes it.
        // Idempotent.
        // rerun: unguarded visible = 1 on ACH_Motto (reverts an admin hiding it; no change otherwise).
        new("84_MottoAchievementVisible.sql", All(Column("achievements", "visible"), Query("SELECT EXISTS (SELECT 1 FROM achievements WHERE group_name = 'ACH_Motto' AND visible = 1) AND NOT EXISTS (SELECT 1 FROM achievements WHERE group_name = 'ACH_Motto' AND visible = 0)")), Rerunnable: false),
        // 85_ColourTileStates: wf_colortile 'pressure_pad' -> 'default' with 7 modes (file:5-6); nothing later changes it. Data-only,
        // idempotent.
        // rerun: UPDATE guarded by interaction_type = pressure_pad.
        new("85_ColourTileStates.sql", Query("SELECT EXISTS (SELECT 1 FROM furniture WHERE item_name = 'wf_colortile' AND interaction_type = 'default' AND interaction_modes_count = 7) AND NOT EXISTS (SELECT 1 FROM furniture WHERE item_name = 'wf_colortile' AND interaction_type = 'pressure_pad')"), Rerunnable: true),
        // 86_RentableBotChatBubble: bots.chat_bubble 2 -> 31 for non-pet bots (file:5). Data-only on a non-content table, idempotent, no
        // marker:
        // post-condition (non-pet bots exist and none has bubble 2). False on FreshInstall states (no bots; rerun is a no-op).
        // NOTE: post-condition probe; a non-pet bot given bubble 2 after 86 makes it false and the rerun sets that bot to 31.
        // rerun: UPDATE guarded by chat_bubble = 2; rerun verified to change nothing.
        new("86_RentableBotChatBubble.sql", All(Column("bots", "chat_bubble"), Column("bots", "ai_type"), Query("SELECT EXISTS (SELECT 1 FROM bots WHERE ai_type <> 'pet') AND NOT EXISTS (SELECT 1 FROM bots WHERE chat_bubble = 2 AND ai_type <> 'pet')")), Rerunnable: true),
        // 87_RecolorableBuildersClubFurni: inserts the 69 *_recolorable furniture rows with interaction 'recolorable' (file:114-140) +
        // pages/offers;
        // 89/91/92 only reference them in Builders Club offers. Idempotent.
        // NOTE: has its own START TRANSACTION/COMMIT (file:15, end).
        // rerun: re-creates deleted furniture rows/pages/offers and rewrites page texts/images (verified: catalog_page_texts/images change).
        new("87_RecolorableBuildersClubFurni.sql", Query("SELECT COUNT(*) > 0 FROM furniture WHERE type = 's' AND item_name = 'bc_block_0_recolorable' AND interaction_type = 'recolorable'"), Rerunnable: false),
        // 88_LauncherTokens: creates user_launcher_tokens (file:6-20); no later file touches it.
        // rerun: CREATE TABLE IF NOT EXISTS only.
        new("88_LauncherTokens.sql", Table("user_launcher_tokens"), Rerunnable: true),
        // 89_BuildersClubCatalog: adds catalog_pages.catalog_mode, creates user_builders_club and items_builders_club (file:10-29);
        // 90-92 extend them, nothing drops them.
        // rerun: re-creates the starter Builders Club pages/offers if they were deleted.
        new("89_BuildersClubCatalog.sql", All(Column("catalog_pages", "catalog_mode"), Table("items_builders_club")), Rerunnable: false),
        // 90_BuildersClubMembership: adds user_builders_club.furni_limit/furni_addon (file:16-18) + membership offers; nothing later touches
        // them.
        // rerun: re-upserts the membership offers (DELETE + INSERT of their products), page texts and re-creates deleted pages.
        new("90_BuildersClubMembership.sql", Column("user_builders_club", "furni_limit"), Rerunnable: false),
        // 91_BuildersClubCatalogTree: replaces the Builders Club tree with Habbo's (root page id 2129066 'furni', BUILDERS_CLUB)
        // (file:21-22, 11724-11733);
        // 92 adds a separate page and does not touch it. Idempotent.
        // rerun: Builders Club catalogue tree replace (verified: catalog_pages/page_texts/page_offers change).
        new("91_BuildersClubCatalogTree.sql", All(Column("catalog_pages", "catalog_mode"), Query("SELECT COUNT(*) > 0 FROM catalog_pages WHERE id = 2129066 AND catalog_mode = 'BUILDERS_CLUB'")), Rerunnable: false),
        // 92_BuildersClubFurnidataOffers: creates the hidden BUILDERS_CLUB page 2100000100 'bc_furnidata_offers' (file:2409-2412) and fills
        // it. Idempotent.
        // rerun: replaces page 2100000100 and its offers (verified: catalog_pages/page_offers change).
        new("92_BuildersClubFurnidataOffers.sql", All(Column("catalog_pages", "catalog_mode"), Query("SELECT COUNT(*) > 0 FROM catalog_pages WHERE id = 2100000100 AND link = 'bc_furnidata_offers' AND catalog_mode = 'BUILDERS_CLUB'")), Rerunnable: false),
        // 93_PickAllBuildersClubCommand: INSERT IGNORE acl_permissions 'command.pickallbc' + grants (file:2-6); nothing later removes it.
        // rerun: re-grants command.pickallbc to every role holding command.pickall if an admin revoked it.
        new("93_PickAllBuildersClubCommand.sql", All(Table("acl_permissions"), Query("SELECT COUNT(*) > 0 FROM acl_permissions WHERE `key` = 'command.pickallbc'")), Rerunnable: false),
        // 94_GlassDoorStates: wf_glassdoor interaction_modes_count -> 2 and its items' extra_data clamped to '0' (file:2-6); last file.
        // Idempotent.
        // rerun: unguarded interaction_modes_count = 2 and items extra_data reset (reverts admin edits; no change otherwise).
        new("94_GlassDoorStates.sql", Query("SELECT EXISTS (SELECT 1 FROM furniture WHERE item_name = 'wf_glassdoor' AND interaction_modes_count = 2) AND NOT EXISTS (SELECT 1 FROM furniture WHERE item_name = 'wf_glassdoor' AND interaction_modes_count <> 2) AND NOT EXISTS (SELECT 1 FROM items i JOIN furniture f ON f.id = i.base_item WHERE f.item_name = 'wf_glassdoor' AND i.extra_data NOT IN ('0', '1'))"), Rerunnable: false),
        // 95_ClaimedProducts: creates user_claimed_products and the pet type 37 chatter (file:8-19); nothing later touches them.
        new("95_ClaimedProducts.sql", Table("user_claimed_products"), Rerunnable: false),
        // 96_ChatStylesAirSelectable: makes chat styles 18, 28, 32, 35, 36 and 38 staff-only (file:4). True when none of those rows is
        // still open to everyone, which includes hotels without the rows (the file changes nothing there).
        new("96_ChatStylesAirSelectable.sql", All(Column("room_chat_styles", "required_permission"), Column("room_chat_styles", "requires_hc"), Query("SELECT NOT EXISTS (SELECT 1 FROM room_chat_styles WHERE id IN (18, 28, 32, 35, 36, 38) AND (required_permission IS NULL OR required_permission <> 'chat.style.staff' OR requires_hc <> 0))")), Rerunnable: false),
        // 97_DailyTasks: creates daily_tasks, daily_task_rewards and user_daily_tasks and seeds the tasks (file:12-70); nothing later
        // touches them.
        new("97_DailyTasks.sql", Table("daily_tasks"), Rerunnable: false),
    };
}
