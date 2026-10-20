namespace Plus.Communication.Packets.Incoming;

public static class ClientPacketHeader
{
    // Handshake
    public const uint InitDiffieHandshakeEvent = 580;
    public const uint CompleteDiffieHandshakeEvent = 1830;
    public const uint UniqueIdEvent = 1531;
    public const uint SSOTicketEvent = 2405;
    public const uint InfoRetrieveEvent = 1955;

    // Avatar
    public const uint CheckUserNameEvent = 2726;
    public const uint ChangeUserNameEvent = 1231;
    public const uint GetWardrobeEvent = 1672;
    public const uint SaveWardrobeOutfitEvent = 3540;
    public const uint GetUserNftWardrobeEvent = 3375; // package_34/GetUserNftWardrobeMessageComposer

    //Preferences
    public const uint SetChatStylePreferenceEvent = 2611;
    public const uint SetChatPreferenceEvent = 65476;

    // Catalog
    public const uint GetCatalogPageEvent = 2955;
    public const uint GetProductOfferEvent = 2810;
    public const uint GetKickbackInfoEvent = 3125;
    public const uint GetClubGiftInfoEvent = 638;
    public const uint PurchaseFromCatalogEvent = 1184;
    public const uint PurchaseFromCatalogAsGiftEvent = 2809;
    public const uint GetBundleDiscountRulesetEvent = 1238;

    // Polls
    public const uint PollStartEvent = 3866;
    public const uint PollRejectEvent = 1406;
    public const uint PollAnswerEvent = 2761;

    // Navigator

    // Messenger
    public const uint GetFriendRequestsEvent = 2798;

    // Quests
    public const uint GetQuestListEvent = 1799;
    public const uint StartQuestEvent = 1356;
    public const uint GetCurrentQuestEvent = 1429;
    public const uint CancelQuestEvent = 1783;

    // Room Avatar
    public const uint ActionEvent = 256;
    public const uint ApplySignEvent = 1785;
    public const uint DanceEvent = 856;
    public const uint SitEvent = 2544;
    public const uint ChangeMottoEvent = 1234;
    public const uint LookToEvent = 2652;
    public const uint DropHandItemEvent = 128;

    // Room Connection
    public const uint OpenFlatConnectionEvent = 541;
    public const uint GoToFlatEvent = 65442;

    // Room Chat
    public const uint ChatEvent = 3708;
    public const uint ShoutEvent = 2666;
    public const uint WhisperEvent = 819;

    // Room Engine

    // Room Settings

    // Room Action

    // Users
    public const uint GetIgnoredUsersEvent = 4;

    // Moderation
    public const uint OpenHelpToolEvent = 3474;
    public const uint CallForHelpPendingCallsDeletedEvent = 1512;
    public const uint ModeratorActionEvent = 3828;
    public const uint ModerationMsgEvent = 3071;
    public const uint ModerationMuteEvent = 2681;
    public const uint ModerationTradeLockEvent = 453;
    public const uint GetModeratorUserRoomVisitsEvent = 1798;
    public const uint ModerationKickEvent = 2252;
    public const uint GetModeratorRoomInfoEvent = 415;
    public const uint GetModeratorUserInfoEvent = 1745;
    public const uint GetModeratorRoomChatlogEvent = 1280;
    public const uint ModerateRoomEvent = 2166;
    public const uint GetModeratorUserChatlogEvent = 250;
    public const uint GetModeratorTicketChatlogsEvent = 2935;
    public const uint ModerationCautionEvent = 610;
    public const uint ModerationBanEvent = 1595;
    public const uint SubmitNewTicketEvent = 652;
    public const uint CloseIssueDefaultActionEvent = 107;

    // Inventory
    public const uint GetCreditsInfoEvent = 962;
    public const uint GetAchievementsEvent = 1817;
    public const uint GetBadgesEvent = 2826;
    public const uint RequestFurniInventoryEvent = 1558;
    public const uint SetActivatedBadgesEvent = 2454;
    public const uint AvatarEffectActivatedEvent = 2510;
    public const uint AvatarEffectSelectedEvent = 3937;

    public const uint InitTradeEvent = 3013;
    public const uint TradingCancelConfirmEvent = 383;
    public const uint TradingModifyEvent = 3235;
    public const uint TradingOfferItemEvent = 31;
    public const uint TradingCancelEvent = 2028;
    public const uint TradingConfirmEvent = 3541;
    public const uint TradingOfferItemsEvent = 1353;
    public const uint TradingRemoveItemEvent = 2206;
    public const uint TradingAcceptEvent = 3544;

    // Register
    public const uint UpdateFigureDataEvent = 1733;

    // Groups
    public const uint GetBadgeEditorPartsEvent = 1492;
    public const uint GetGroupCreationWindowEvent = 2294;
    public const uint GetGroupFurniSettingsEvent = 2001;
    public const uint DeclineGroupMembershipEvent = 26;
    public const uint JoinGroupEvent = 476;
    public const uint UpdateGroupColoursEvent = 187;
    public const uint SetGroupFavouriteEvent = 1679;
    public const uint GetGroupMembersEvent = 3411;

    // Group Forums
    public const uint PostGroupContentEvent = 1945;
    public const uint GetForumStatsEvent = 3167;
    public const uint GetForumThreadEvent = 1491;
    public const uint UpdateForumReadMarkersEvent = 2296;
    public const uint GetForumsUnreadCountEvent = 1525;

    // Sound


    // Ambassador

    public const uint AmbassadorAlertEvent = 2253;

    public const uint RemoveMyRightsEvent = 506;
    public const uint GiveHandItemEvent = 3719;
    public const uint GoToHotelViewEvent = 76;
    public const uint GetRoomFilterListEvent = 3791;
    public const uint GetPromoArticlesEvent = 2766;
    public const uint ModifyWhoCanRideHorseEvent = 3850;
    public const uint RemoveFriendEvent = 2173;
    public const uint RefreshCampaignEvent = 443;
    public const uint AcceptFriendEvent = 2328;
    public const uint YouTubeVideoInformationEvent = 65401; // No AIR WIN63-202610151745-318700621 composer; 2488 is TryPhoneNumber (package_144/class_2849)
    public const uint FollowFriendEvent = 2669;
    public const uint SaveBotActionEvent = 2361;
    public const uint LetUserInEvent = 2207;
    public const uint GetMarketplaceItemStatsEvent = 2214;
    public const uint GetSellablePetPalettesEvent = 3648;
    public const uint OpenCampaignCalendarDoorAsStaffEvent = 543;
    public const uint SetUIFlagsEvent = 451;
    public const uint DeleteRoomEvent = 3863;
    public const uint SetSoundSettingsEvent = 3621;
    public const uint InitializeGameCenterEvent = 65468;
    public const uint RedeemOfferCreditsEvent = 3418;
    public const uint FriendListUpdateEvent = 2133;
    public const uint FriendFurniConfirmLockEvent = 653;
    public const uint UseHabboWheelEvent = 3847;
    public const uint SaveRoomSettingsEvent = 2440;
    public const uint ToggleMoodlightEvent = 2168;
    public const uint GetDailyQuestEvent = 2152;
    public const uint SetMannequinNameEvent = 1281;
    public const uint OneWayGateEvent = 1288;
    public const uint EventTrackerEvent = 65429;
    public const uint GetOccupiedTilesEvent = 2900;
    public const uint PickUpPetEvent = 505;
    public const uint GetPetInventoryEvent = 3483;
    public const uint GetRoomEntryTileEvent = 2476;
    public const uint GetOwnOffersEvent = 1069;
    public const uint CheckPetNameEvent = 3751;
    public const uint SetUserFocusPreferenceEvent = 1239;
    public const uint SubmitBullyReportEvent = 1078;
    public const uint RemoveRightsEvent = 2002;
    public const uint MakeOfferEvent = 1517;
    public const uint KickUserEvent = 502;
    public const uint GetRoomSettingsEvent = 2096;
    public const uint GetThreadsListDataEvent = 655;
    public const uint GetForumUserProfileEvent = 1597;
    public const uint OpenWiredEvent = 1757;
    public const uint WiredMonitorRequestEvent = 10021;
    public const uint WiredRoomLogsPageEvent = 964;
    public const uint WiredRoomSettingsRequestEvent = 10022;
    public const uint WiredRoomSettingsSaveEvent = 10023;
    public const uint WiredGetRoomSettingsEvent = 3215;
    public const uint WiredSetRoomSettingsEvent = 2035;
    public const uint ClickFurniEvent = 1405;
    public const uint ClickUserEvent = 10020;
    public const uint WiredUserVariablesRequestEvent = 10024;
    public const uint WiredUserVariableUpdateEvent = 10025;
    public const uint WiredUserVariableManageEvent = 10026;
    public const uint WiredVariableHoldersPageEvent = 65307;
    public const uint WiredVariableHoldersRequestEvent = 65441;
    public const uint WiredVariableHashesEvent = 2090;
    public const uint WiredAllVariablesRequestEvent = 2873;
    public const uint SaveWiredEffectConfigEvent = 3006;
    public const uint ApplySnapshotEvent = 564; // class_3039, ApplySnapshot
    public const uint SaveWiredSelectorConfigEvent = 562;
    public const uint SaveWiredAddonConfigEvent = 3596;
    public const uint SaveWiredVariableConfigEvent = 3471;
    public const uint GetRoomEntryDataEvent = 65460;
    public const uint JoinQueueEvent = 65471;
    public const uint CanCreateRoomEvent = 321;
    public const uint SetTonerEvent = 3383;
    public const uint RecolorFurnitureEvent = 569; // package_100/class_2414
    public const uint SaveWiredTriggerConfigEvent = 3361;
    public const uint PlaceBotEvent = 693;
    public const uint MoveEntityInFlatEvent = 1;
    public const uint GetRelationshipsEvent = 24;
    public const uint SetMessengerInviteStatusEvent = 1342;
    public const uint UseFurnitureEvent = 3416;
    public const uint GetUserFlatCatsEvent = 335;
    public const uint AssignRightsEvent = 1349;
    public const uint GetRoomBannedUsersEvent = 1476;
    public const uint ReleaseTicketEvent = 3764;
    public const uint OpenPlayerProfileEvent = 708;
    public const uint GetSanctionStatusEvent = 65447;
    public const uint CreditFurniRedeemEvent = 2960;
    public const uint DisconnectEvent = 850;
    public const uint PickupObjectEvent = 3593;
    public const uint FindRandomFriendingRoomEvent = 1951;
    public const uint UseSellableClothingEvent = 1329;
    public const uint MoveObjectEvent = 2858;
    public const uint GetFurnitureAliasesEvent = 1243;
    public const uint TakeAdminRightsEvent = 1110;
    public const uint ModifyRoomFilterListEvent = 3008;
    public const uint MoodlightUpdateEvent = 1224;
    public const uint GetPetTrainingPanelEvent = 2159;
    public const uint GetSongInfoEvent = 2304;
    public const uint UseWallItemEvent = 222;
    public const uint GetTalentTrackEvent = 766;
    public const uint GiveAdminRightsEvent = 2461;
    public const uint GetCatalogIndexEvent = 2041;
    public const uint BuildersClubPlaceRoomItemEvent = 141; // package_16/class_2016
    public const uint BuildersClubPlaceWallItemEvent = 3269; // package_16/class_1795
    public const uint GetGuideReportingStatusEvent = 2182; // class_2256, GetGuideReportingStatus
    public const uint CancelOfferEvent = 511;
    public const uint SaveWiredConditionConfigEvent = 2814;
    public const uint RedeemVoucherEvent = 1563;
    public const uint ThrowDiceEvent = 3342;
    public const uint CraftSecretEvent = 3614;
    public const uint GetGameListEvent = 65391;
    public const uint SetRelationshipEvent = 1129;
    public const uint RequestFriendEvent = 971;
    public const uint MemoryPerformanceEvent = 3956;
    public const uint ToggleYouTubeVideoEvent = 2442;
    public const uint SetMannequinFigureEvent = 1361;
    public const uint SetClothingChangeDataEvent = 2545; // package_66.class_3476
    public const uint GetEventCategoriesEvent = 3413;
    public const uint DeleteGroupThreadEvent = 3333;
    public const uint PurchaseGroupEvent = 786;
    public const uint MessengerInitEvent = 1755;
    public const uint CancelTypingEvent = 3076;
    public const uint GetMoodlightConfigEvent = 1671;
    public const uint GetGroupInfoEvent = 3533;
    public const uint CreateFlatEvent = 3409;
    public const uint LatencyPingRequestEvent = 1235;
    public const uint GetSelectedBadgesEvent = 500;
    public const uint AddStickyNoteEvent = 215;
    public const uint RideHorseEvent = 3920;
    public const uint InitializeNewNavigatorEvent = 3637;
    public const uint GetForumsListDataEvent = 409;
    public const uint ToggleMuteToolEvent = 1793;
    public const uint UpdateGroupIdentityEvent = 982;
    public const uint UpdateStickyNoteEvent = 3085;
    public const uint UnbanUserFromRoomEvent = 1328;
    public const uint UnignoreUserEvent = 1650;
    public const uint OpenGiftEvent = 29;
    public const uint ApplyDecorationEvent = 2392;
    public const uint ScrGetUserInfoEvent = 3073;
    public const uint RemoveGroupMemberEvent = 1246;
    public const uint ConfirmRemoveGroupMemberEvent = 43;
    public const uint DiceOffEvent = 2795;
    public const uint YouTubeGetNextVideo = 247;
    public const uint RemoveFavouriteRoomEvent = 2463;
    public const uint RespectUserEvent = 2993;
    public const uint AddFavouriteRoomEvent = 147;
    public const uint DeclineFriendEvent = 1055;
    public const uint StartTypingEvent = 21;
    public const uint GetGroupFurniConfigEvent = 2066;
    public const uint SendRoomInviteEvent = 1093;
    public const uint RemoveAllRightsEvent = 1435;
    public const uint GetYouTubeTelevisionEvent = 3208;
    public const uint FindNewFriendsEvent = 2493;
    public const uint GetPromotableRoomsEvent = 2774;
    public const uint GetBotInventoryEvent = 1420;
    public const uint GetRentableSpaceEvent = 3427;
    public const uint OpenBotActionEvent = 439;
    public const uint OpenCampaignCalendarDoorEvent = 2585;
    public const uint DeleteGroupPostEvent = 740;
    public const uint UpdateGroupBadgeEvent = 1177;
    public const uint PlaceObjectEvent = 3538;
    public const uint RemoveGroupFavouriteEvent = 3845;
    public const uint UpdateNavigatorSettingsEvent = 3126;
    public const uint CheckGnomeNameEvent = 3051;
    public const uint NavigatorSearchEvent = 2557;
    public const uint GetPetInformationEvent = 2367;
    public const uint GetGuestRoomEvent = 2918;
    public const uint UpdateThreadEvent = 3139;
    public const uint AcceptGroupMembershipEvent = 1439;
    public const uint GetMarketplaceConfigurationEvent = 1624;
    public const uint Game2GetWeeklyLeaderboardEvent = 1469;
    public const uint BuyOfferEvent = 1619;
    public const uint RemoveSaddleFromHorseEvent = 1481;
    public const uint GiveRoomScoreEvent = 1876;
    public const uint GetHabboClubWindowEvent = 1646;
    public const uint DeleteStickyNoteEvent = 484;
    public const uint MuteUserEvent = 3797;
    public const uint ApplyHorseEffectEvent = 880;
    public const uint ClientHelloEvent = 4000; //4000
    public const uint OnBullyClickEvent = 65455;
    public const uint HabboSearchEvent = 790;
    public const uint PickTicketEvent = 2631;
    public const uint GetGiftWrappingConfigurationEvent = 775;
    public const uint GetCraftingRecipesAvailableEvent = 1692;
    public const uint GetThreadDataEvent = 2489;
    public const uint ManageGroupEvent = 722;
    public const uint PlacePetEvent = 3346;
    public const uint MovePetEvent = 378;
    public const uint EditRoomPromotionEvent = 1477;
    public const uint UpdateFloorPropertiesEvent = 1044;
    public const uint MoveWallItemEvent = 2481;
    public const uint VersionCheckEvent = 2541;
    public const uint PongEvent = 2653;
    public const uint DeleteGroupEvent = 3966;
    public const uint UpdateGroupSettingsEvent = 2636;
    public const uint GetRecyclerPrizesEvent = 2393;
    public const uint PurchaseRoomAdEvent = 1451;
    public const uint PickUpBotEvent = 2589;
    public const uint GetOffersEvent = 2813;
    public const uint GetHabboGroupBadgesEvent = 2397;
    public const uint GetUserTagsEvent = 65382;
    public const uint GetGameAchievementsEvent = 65475;
    public const uint GetCatalogRoomPromotionEvent = 1444;
    public const uint MoveAvatarEvent = 2348;
    public const uint SaveBrandingItemEvent = 2787;
    public const uint SaveEnforcedCategorySettingsEvent = 1848;
    public const uint RespectPetEvent = 3068;
    public const uint GetMarketplaceCanMakeOfferEvent = 150;
    public const uint UpdateMagicTileAdjacentEvent = 65449;
    public const uint UpdateMagicTileEvent = 559;
    public const uint GetStickyNoteEvent = 895;
    public const uint IgnoreUserEvent = 2333;
    public const uint BanUserEvent = 362;
    public const uint UpdateForumSettingsEvent = 1381;
    public const uint GetRoomRightsEvent = 111;
    public const uint SendMsgEvent = 3489;
    public const uint CloseTicketEvent = 1372;

    //NotImplemented

    //Camera
    //public const uint InitCameraEvent =;
    //public const uint PhotoCompetitionEvent =;
    //public const uint PublishPhotoEvent =;
    //public const uint PurchasePhotoEvent =;
    //public const uint RenderRoomEvent =;
    //public const uint RenderRoomThumbnailEvent =;

    //campaign
    //public const uint OpenCampaignCalendarDoorAsStaffEvent =;
    //public const uint OpenCampaignCalendarDoorEvent =;

    //catalog
    public const uint GetHabboClubExtendOfferEvent = 1317;
    public const uint PurchaseBasicMembershipExtensionEvent = 308;
    public const uint PurchaseVipMembershipExtensionEvent = 1889;
    public const uint SelectClubGiftEvent = 194;


    //recycler
    public const uint GetRecyclerStatusEvent = 998;
    public const uint RecyclerRecycleEvent = 498;


    //FriendList
    //public const uint FriendFurniConfirmLockEvent =;
    //public const uint VisitUserEvent =;

    //crafting
    public const uint CraftEvent = 823;
    //public const uint CraftSecretEvent =;
    public const uint GetCraftableProductsEvent = 2967;
    public const uint GetCraftingRecipeEvent = 3576;
    //public const uint GetCraftingRecipesAvailableEvent =;

    // SnowStorm (AIR Game2 payloads); internal IDs match Volt's wire IDs.
    public const uint Game2CheckGameDirectoryStatusEvent = 1652;
    public const uint Game2GetAccountGameStatusEvent = 3461;
    public const uint Game2QuickJoinEvent = 6012;
    public const uint Game2LeaveLobbyEvent = 6013;
    public const uint Game2LoadStageReadyEvent = 3983;
    public const uint Game2ExitGameEvent = 3101;
    public const uint Game2GameChatEvent = 884;
    public const uint Game2PlayAgainEvent = 1296;
    public const uint Game2SetUserMoveTargetEvent = 6003;
    public const uint Game2ThrowSnowballAtPositionEvent = 6004;
    public const uint Game2ThrowSnowballAtHumanEvent = 6005;
    public const uint Game2MakeSnowballEvent = 6006;
    public const uint Game2RequestFullStatusUpdateEvent = 2922;
    public const uint Game2GetTotalLeaderboardEvent = 6027;
    public const uint Game2GetFriendsLeaderboardEvent = 6028;
    public const uint Game2GetWeeklyFriendsLeaderboardEvent = 1051;
    public const uint Game2GetTotalGroupLeaderboardEvent = 1318;
    public const uint Game2GetWeeklyGroupLeaderboardEvent = 3578;
    public const uint GetSnowWarGameTokensOfferEvent = 1205;
    public const uint PurchaseSnowWarGameTokensOfferEvent = 1387;




    public const uint TriggerHabbiconEvent = 3244;
    public const uint GetHabbiconShopDataEvent = 1739;
    public const uint GetHabbiconInfoEvent = 1484;
    public const uint BuyHabbiconEvent = 3434;
    public const uint BuyHabbiconCollectionEvent = 1928;
    public const uint ClaimHabbiconEvent = 1577;
    public const uint FavoriteHabbiconEvent = 1427;
    public const uint UnfavoriteHabbiconEvent = 3728;
    public const uint UnseenResetCategoryEvent = 65428;
    public const uint UnseenResetItemsEvent = 65459;
    public const uint SendMessengerMessageEvent = 4902;
    // Camera internal IDs; wire IDs are revision-specific.
    public const uint InitCameraEvent = 1766;
    public const uint PhotoCompetitionEvent = 1705;
    public const uint PublishPhotoEvent = 2130;
    public const uint PurchasePhotoEvent = 2872;
    public const uint RenderRoomEvent = 2284;
    public const uint RenderRoomThumbnailEvent = 2854;
    public const uint GetRewardTracksEvent = 9450;
    public const uint ClaimRewardTrackPrizeEvent = 3959;
    public const uint PurchaseRewardTrackPremiumEvent = 2482;
    // Daily tasks, AIR WIN63-202610151745-318700621 (quest/dailytasks/DailyTasksController.as).
    public const uint GetDailyTasksEvent = 4100; // package_24.class_1820
    public const uint ClaimDailyTaskEvent = 4101; // package_24.class_2568
    public const uint GetQuizQuestionsEvent = 640;
    public const uint PostQuizAnswersEvent = 3024;
    // Room music
    public const uint AddJukeboxDiskEvent = 3582;
    public const uint RemoveJukeboxDiskEvent = 1360;
    public const uint GetJukeboxPlaylistEvent = 3095;
    public const uint GetSoundMachinePlaylistEvent = 3293;
    public const uint GetUserSongDisksEvent = 1654;
    public const uint GetNowPlayingEvent = 900;
    public const uint GetOfficialSongIdEvent = 3214;
    public const uint WiredVariableInspectionRequestEvent = 10112;
    public const uint WiredUserVariableUpdate64Event = 10110;
    public const uint WiredUserVariableManage64Event = 10111;
    public const uint WiredUserVariablesRequest64Event = 10113;
    public const uint WiredVariableHoldersRequest64Event = 10114;
    public const uint WiredVariableHoldersPage64Event = 10115;
    // Wired chests and wired trades; see reviews/chest-protocol-map.md.
    public const uint ChestOpenEvent = 3725; // package_95.class_2556
    public const uint ChestCloseEvent = 2212; // package_95.class_2991
    public const uint ChestStartDepositEvent = 1465; // package_95.class_3680
    public const uint ChestWithdrawAllEvent = 625; // package_95.class_3893
    public const uint ChestWithdrawFurniEvent = 2058; // package_95.class_2400
    public const uint ChestWithdrawCoinsEvent = 1346; // package_95.class_3317
    public const uint ChestSaveOptionsEvent = 668; // package_95.class_3287
    public const uint ChestSavePreferencesEvent = 1241; // package_95.class_3106
    public const uint ChestSaveNotificationsEvent = 1896; // package_95.class_2759
    public const uint WiredChestOfferItemsEvent = 1549; // package_186.class_2968
    public const uint WiredChestAcceptEvent = 403; // package_186.class_3406
    public const uint WiredChestCancelEvent = 3365; // package_186.class_3271
    public const uint ChestUpgradeEvent = 894; // package_95.class_3733
    public const uint WiredChestLockEvent = 3745; // package_95.class_3520

    // October wired boxes (r88).
    public const uint WiredWebApiKeyRequestEvent = 3497; // class_2630
    public const uint WiredVariableShareSynchronizeEvent = 2698; // class_3060
    public const uint WiredVariableLeaderboardSubscribeEvent = 3168; // package_221.class_3719
    public const uint SaveWiredLabelTextEvent = 2968; // class_2809
    public const uint SaveWiredLabelSettingsEvent = 2701; // class_3231
    public const uint WiredLinkRequestConfigEvent = 1176; // package_129.class_2728
    public const uint WiredLinkSaveSelectionEvent = 9; // package_129.class_2545
    public const uint WiredLinkCreateNetworkEvent = 3220; // package_129.class_3727
    public const uint WiredLinkRenameNetworkEvent = 1669; // package_129.class_3853
    public const uint WiredLinkDeleteNetworkEvent = 2355; // package_129.class_3634
    public const uint WiredLinkGetConnectionsEvent = 2950; // package_129.class_3536
    public const uint GetWiredContractContentsEvent = 1373; // package_193.class_3900
    public const uint UpdateWiredContractEvent = 3632; // package_193.class_3040
    // Wired monitor (wiredmenu composers); the monitor tab sends both requests together (WiredMenuMonitorTab.requestData).
    public const uint GetWiredRoomStatsEvent = 2860; // wiredmenu.class_3345
    public const uint GetWiredErrorLogsEvent = 3216; // wiredmenu.class_2587
    public const uint ClearWiredErrorLogsEvent = 3246; // wiredmenu.class_3251
    // The inspection tab's value edit, create and delete (WiredMenuInspectionTab.as:193, 249, 265).
    public const uint WiredSetObjectVariableValueEvent = 3165; // wiredmenu.class_3782
    // The inspection tab's read of one object's variables (WiredMenuInspectionTab.requestVariablesForObject).
    public const uint WiredGetVariablesForObjectEvent = 2612; // wiredmenu.class_3018

    // Free product claims (cursed dragon adoption) and the new-feature promo expiry, AIR WIN63-202610151745-318700621.
    public const uint HasClaimedProductEvent = 628; // package_143.class_3538
    public const uint ClaimProductEvent = 152; // package_143.class_2610
    public const uint GetSecondsUntilEvent = 903; // package_146.class_3739
}
