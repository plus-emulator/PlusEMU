namespace Plus.Communication.Packets.Incoming;

public static class ClientPacketHeader
{
    // Handshake
    public const uint InitDiffieHandshakeEvent = 3034;
    public const uint CompleteDiffieHandshakeEvent = 3010;
    public const uint UniqueIdEvent = 3935;
    public const uint SSOTicketEvent = 591;
    public const uint InfoRetrieveEvent = 3209;

    // Avatar
    public const uint CheckUserNameEvent = 2451;
    public const uint ChangeUserNameEvent = 1837;
    public const uint GetWardrobeEvent = 832;
    public const uint SaveWardrobeOutfitEvent = 944;
    public const uint GetUserNftWardrobeEvent = 1320; // package_34/GetUserNftWardrobeMessageComposer
    public const uint GetSelectedNftWardrobeOutfitEvent = 3707; // package_34/class_3910
    public const uint SaveUserNftWardrobeEvent = 2036; // package_34/SaveUserNftWardrobeMessageComposer

    //Preferences
    public const uint SetChatStylePreferenceEvent = 1615;
    public const uint SetChatPreferenceEvent = 65476;

    // Catalog
    public const uint GetCatalogPageEvent = 3271;
    public const uint GetProductOfferEvent = 355;
    public const uint GetKickbackInfoEvent = 3560;
    public const uint GetClubGiftInfoEvent = 1902;
    public const uint PurchaseFromCatalogEvent = 3301;
    public const uint PurchaseFromCatalogAsGiftEvent = 85;
    public const uint GetBundleDiscountRulesetEvent = 3392;

    // Polls
    public const uint PollStartEvent = 3559;
    public const uint PollRejectEvent = 2734;
    public const uint PollAnswerEvent = 2209;

    // Navigator

    // Messenger
    public const uint GetFriendRequestsEvent = 1915;

    // Quests
    public const uint GetQuestListEvent = 3621;
    public const uint StartQuestEvent = 2462;
    public const uint GetCurrentQuestEvent = 2953;
    public const uint CancelQuestEvent = 402;

    // Room Avatar
    public const uint ActionEvent = 555;
    public const uint ApplySignEvent = 730;
    public const uint DanceEvent = 848;
    public const uint SitEvent = 3797;
    public const uint ChangeMottoEvent = 1239;
    public const uint LookToEvent = 1331;
    public const uint DropHandItemEvent = 3649;

    // Room Connection
    public const uint OpenFlatConnectionEvent = 2263;
    public const uint GoToFlatEvent = 65442;

    // Room Chat
    public const uint ChatEvent = 1562;
    public const uint ShoutEvent = 2558;
    public const uint WhisperEvent = 3420;

    // Room Engine

    // Room Settings

    // Room Action

    // Users
    public const uint GetIgnoredUsersEvent = 2724;

    // Moderation
    public const uint OpenHelpToolEvent = 2680;
    public const uint CallForHelpPendingCallsDeletedEvent = 423;
    public const uint ModeratorActionEvent = 2504;
    public const uint ModerationMsgEvent = 3415;
    public const uint ModerationMuteEvent = 820;
    public const uint ModerationTradeLockEvent = 1750;
    public const uint GetModeratorUserRoomVisitsEvent = 3902;
    public const uint ModerationKickEvent = 804;
    public const uint GetModeratorRoomInfoEvent = 2518;
    public const uint GetModeratorUserInfoEvent = 2405;
    public const uint GetModeratorRoomChatlogEvent = 1651;
    public const uint ModerateRoomEvent = 2711;
    public const uint GetModeratorUserChatlogEvent = 740;
    public const uint GetModeratorTicketChatlogsEvent = 319;
    public const uint ModerationCautionEvent = 3447;
    public const uint ModerationBanEvent = 874;
    public const uint SubmitNewTicketEvent = 3118;
    public const uint CloseIssueDefaultActionEvent = 2350;

    // Inventory
    public const uint GetCreditsInfoEvent = 2740;
    public const uint GetAchievementsEvent = 101;
    public const uint GetBadgesEvent = 2347;
    public const uint RequestFurniInventoryEvent = 2331;
    public const uint SetActivatedBadgesEvent = 1922;
    public const uint AvatarEffectActivatedEvent = 152;
    public const uint AvatarEffectSelectedEvent = 273;

    public const uint InitTradeEvent = 2770;
    public const uint TradingCancelConfirmEvent = 229;
    public const uint TradingModifyEvent = 2596;
    public const uint TradingOfferItemEvent = 1741;
    public const uint TradingCancelEvent = 1766;
    public const uint TradingConfirmEvent = 2278;
    public const uint TradingOfferItemsEvent = 3642;
    public const uint TradingRemoveItemEvent = 1675;
    public const uint TradingAcceptEvent = 3180;

    // Register
    public const uint UpdateFigureDataEvent = 1011;

    // Groups
    public const uint GetBadgeEditorPartsEvent = 772;
    public const uint GetGroupCreationWindowEvent = 3163;
    public const uint GetGroupFurniSettingsEvent = 2582;
    public const uint DeclineGroupMembershipEvent = 1419;
    public const uint JoinGroupEvent = 2551;
    public const uint UpdateGroupColoursEvent = 2214;
    public const uint SetGroupFavouriteEvent = 2919;
    public const uint GetGroupMembersEvent = 2119;

    // Group Forums
    public const uint PostGroupContentEvent = 639;
    public const uint GetForumStatsEvent = 1855;
    public const uint GetForumThreadEvent = 3520;
    public const uint UpdateForumReadMarkersEvent = 2541;
    public const uint GetForumsUnreadCountEvent = 3613;

    // Sound


    // Ambassador

    public const uint AmbassadorAlertEvent = 1281;

    public const uint RemoveMyRightsEvent = 3924;
    public const uint GiveHandItemEvent = 195;
    public const uint GoToHotelViewEvent = 2818;
    public const uint GetRoomFilterListEvent = 2534;
    public const uint GetPromoArticlesEvent = 2872;
    public const uint ModifyWhoCanRideHorseEvent = 2569;
    public const uint RemoveFriendEvent = 3098;
    public const uint RefreshCampaignEvent = 720;
    public const uint AcceptFriendEvent = 1797;
    public const uint YouTubeVideoInformationEvent = 65401; // No AIR WIN63-202610151745-318700621 composer; 2488 is TryPhoneNumber (package_144/class_2849)
    public const uint FollowFriendEvent = 394;
    public const uint SaveBotActionEvent = 3967;
    public const uint LetUserInEvent = 3316;
    public const uint GetMarketplaceItemStatsEvent = 3428;
    public const uint GetSellablePetPalettesEvent = 2333;
    public const uint OpenCampaignCalendarDoorAsStaffEvent = 1025;
    public const uint SetUIFlagsEvent = 2194;
    public const uint DeleteRoomEvent = 3667;
    public const uint SetSoundSettingsEvent = 2435;
    public const uint InitializeGameCenterEvent = 65468;
    public const uint RedeemOfferCreditsEvent = 2939;
    public const uint FriendListUpdateEvent = 3665;
    public const uint FriendFurniConfirmLockEvent = 1337;
    public const uint UseHabboWheelEvent = 2608;
    public const uint SaveRoomSettingsEvent = 2412;
    public const uint ToggleMoodlightEvent = 2253;
    public const uint GetDailyQuestEvent = 2399;
    public const uint SetMannequinNameEvent = 2211;
    public const uint OneWayGateEvent = 2578;
    public const uint EventTrackerEvent = 65429;
    public const uint GetOccupiedTilesEvent = 1301;
    public const uint PickUpPetEvent = 3855;
    public const uint GetPetInventoryEvent = 3044;
    public const uint GetRoomEntryTileEvent = 3801;
    public const uint GetOwnOffersEvent = 3444;
    public const uint CheckPetNameEvent = 3456;
    public const uint SetUserFocusPreferenceEvent = 2721;
    public const uint SubmitBullyReportEvent = 1635;
    public const uint RemoveRightsEvent = 3028;
    public const uint MakeOfferEvent = 221;
    public const uint KickUserEvent = 313;
    public const uint GetRoomSettingsEvent = 2584;
    public const uint GetThreadsListDataEvent = 3449;
    public const uint GetForumUserProfileEvent = 116;
    public const uint OpenWiredEvent = 2497;
    public const uint WiredMonitorRequestEvent = 10021;
    public const uint WiredRoomLogsPageEvent = 2730;
    public const uint WiredRoomSettingsRequestEvent = 10022;
    public const uint WiredRoomSettingsSaveEvent = 10023;
    public const uint WiredGetRoomSettingsEvent = 3320;
    public const uint WiredSetRoomSettingsEvent = 2172;
    public const uint WiredMenuPermissionsSaveEvent = 65465;
    public const uint ClickFurniEvent = 1192;
    public const uint ClickUserEvent = 10020;
    public const uint WiredUserVariablesRequestEvent = 10024;
    public const uint WiredUserVariableUpdateEvent = 10025;
    public const uint WiredUserVariableManageEvent = 10026;
    public const uint WiredVariableHoldersPageEvent = 65307;
    public const uint WiredVariableHoldersRequestEvent = 65441;
    public const uint WiredVariableHashesEvent = 1219;
    public const uint WiredAllVariablesRequestEvent = 1467;
    public const uint SaveWiredEffectConfigEvent = 2397;
    public const uint ApplySnapshotEvent = 787; // class_3039, ApplySnapshot
    public const uint SaveWiredSelectorConfigEvent = 3071;
    public const uint SaveWiredAddonConfigEvent = 3963;
    public const uint SaveWiredVariableConfigEvent = 138;
    public const uint GetRoomEntryDataEvent = 65460;
    public const uint JoinQueueEvent = 65471;
    public const uint CanCreateRoomEvent = 988;
    public const uint SetTonerEvent = 3802;
    public const uint RecolorFurnitureEvent = 3752; // package_100/class_2414
    public const uint SaveWiredTriggerConfigEvent = 2487;
    public const uint PlaceBotEvent = 1994;
    public const uint MoveEntityInFlatEvent = 2508;
    public const uint GetRelationshipsEvent = 472;
    public const uint SetMessengerInviteStatusEvent = 2388;
    public const uint UseFurnitureEvent = 1324;
    public const uint GetUserFlatCatsEvent = 2765;
    public const uint AssignRightsEvent = 2537;
    public const uint GetRoomBannedUsersEvent = 2718;
    public const uint ReleaseTicketEvent = 2591;
    public const uint OpenPlayerProfileEvent = 3620;
    public const uint GetSanctionStatusEvent = 65447;
    public const uint CreditFurniRedeemEvent = 301;
    public const uint DisconnectEvent = 2987;
    public const uint PickupObjectEvent = 2287;
    public const uint FindRandomFriendingRoomEvent = 838;
    public const uint UseSellableClothingEvent = 1734;
    public const uint MoveObjectEvent = 687;
    public const uint GetFurnitureAliasesEvent = 3830;
    public const uint TakeAdminRightsEvent = 1279;
    public const uint ModifyRoomFilterListEvent = 2725;
    public const uint MoodlightUpdateEvent = 2027;
    public const uint GetPetTrainingPanelEvent = 3485;
    public const uint GetSongInfoEvent = 2612;
    public const uint UseWallItemEvent = 3540;
    public const uint GetTalentTrackEvent = 3551;
    public const uint GiveAdminRightsEvent = 2249;
    public const uint GetCatalogIndexEvent = 3401;
    public const uint BuildersClubPlaceRoomItemEvent = 3501; // package_16/class_2016
    public const uint BuildersClubPlaceWallItemEvent = 428; // package_16/class_1795
    public const uint GetGuideReportingStatusEvent = 1348; // class_2256, GetGuideReportingStatus
    public const uint CancelOfferEvent = 872;
    public const uint SaveWiredConditionConfigEvent = 3498;
    public const uint RedeemVoucherEvent = 1603;
    public const uint ThrowDiceEvent = 2485;
    public const uint CraftSecretEvent = 1178;
    public const uint GetGameListEvent = 65391;
    public const uint SetRelationshipEvent = 1307;
    public const uint RequestFriendEvent = 3236;
    public const uint MemoryPerformanceEvent = 1121;
    public const uint ToggleYouTubeVideoEvent = 2720;
    public const uint SetMannequinFigureEvent = 2169;
    public const uint SetClothingChangeDataEvent = 2321; // package_66.class_3476
    public const uint GetEventCategoriesEvent = 897;
    public const uint DeleteGroupThreadEvent = 614;
    public const uint PurchaseGroupEvent = 2440;
    public const uint MessengerInitEvent = 204;
    public const uint CancelTypingEvent = 2654;
    public const uint GetMoodlightConfigEvent = 218;
    public const uint GetGroupInfoEvent = 54;
    public const uint CreateFlatEvent = 3852;
    public const uint LatencyPingRequestEvent = 3730;
    public const uint GetSelectedBadgesEvent = 3303;
    public const uint AddStickyNoteEvent = 2528;
    public const uint RideHorseEvent = 3638;
    public const uint InitializeNewNavigatorEvent = 1479;
    public const uint GetForumsListDataEvent = 3407;
    public const uint ToggleMuteToolEvent = 1825;
    public const uint UpdateGroupIdentityEvent = 3155;
    public const uint UpdateStickyNoteEvent = 2148;
    public const uint UnbanUserFromRoomEvent = 3128;
    public const uint UnignoreUserEvent = 1047;
    public const uint OpenGiftEvent = 1478;
    public const uint ApplyDecorationEvent = 1992;
    public const uint ScrGetUserInfoEvent = 176;
    public const uint RemoveGroupMemberEvent = 2624;
    public const uint ConfirmRemoveGroupMemberEvent = 2804;
    public const uint DiceOffEvent = 2902;
    public const uint YouTubeGetNextVideo = 43;
    public const uint RemoveFavouriteRoomEvent = 3165;
    public const uint RespectUserEvent = 1402;
    public const uint AddFavouriteRoomEvent = 2361;
    public const uint DeclineFriendEvent = 1009;
    public const uint StartTypingEvent = 2166;
    public const uint GetGroupFurniConfigEvent = 757;
    public const uint SendRoomInviteEvent = 3763;
    public const uint RemoveAllRightsEvent = 3148;
    public const uint GetYouTubeTelevisionEvent = 2294;
    public const uint FindNewFriendsEvent = 1808;
    public const uint GetPromotableRoomsEvent = 2413;
    public const uint GetBotInventoryEvent = 815;
    public const uint GetRentableSpaceEvent = 3306;
    public const uint OpenBotActionEvent = 1748;
    public const uint OpenCampaignCalendarDoorEvent = 2365;
    public const uint DeleteGroupPostEvent = 3853;
    public const uint UpdateGroupBadgeEvent = 3491;
    public const uint PlaceObjectEvent = 100;
    public const uint RemoveGroupFavouriteEvent = 1118;
    public const uint UpdateNavigatorSettingsEvent = 2548;
    public const uint CheckGnomeNameEvent = 124;
    public const uint NavigatorSearchEvent = 438;
    public const uint GetPetInformationEvent = 1949;
    public const uint GetGuestRoomEvent = 633;
    public const uint UpdateThreadEvent = 450;
    public const uint AcceptGroupMembershipEvent = 1571;
    public const uint GetMarketplaceConfigurationEvent = 763;
    public const uint Game2GetWeeklyLeaderboardEvent = 2215;
    public const uint BuyOfferEvent = 3262;
    public const uint RemoveSaddleFromHorseEvent = 3317;
    public const uint GiveRoomScoreEvent = 3748;
    public const uint GetHabboClubWindowEvent = 3899;
    public const uint DeleteStickyNoteEvent = 1267;
    public const uint MuteUserEvent = 2185;
    public const uint ApplyHorseEffectEvent = 726;
    public const uint ClientHelloEvent = 4000; //4000
    public const uint OnBullyClickEvent = 65455;
    public const uint HabboSearchEvent = 3208;
    public const uint PickTicketEvent = 1535;
    public const uint GetGiftWrappingConfigurationEvent = 2545;
    public const uint GetCraftingRecipesAvailableEvent = 3727;
    public const uint GetThreadDataEvent = 2082;
    public const uint ManageGroupEvent = 3940;
    public const uint PlacePetEvent = 596;
    public const uint MovePetEvent = 819;
    public const uint EditRoomPromotionEvent = 1789;
    public const uint UpdateFloorPropertiesEvent = 3509;
    public const uint MoveWallItemEvent = 1013;
    public const uint VersionCheckEvent = 3897;
    public const uint PongEvent = 2001;
    public const uint DeleteGroupEvent = 2097;
    public const uint UpdateGroupSettingsEvent = 1944;
    public const uint GetRecyclerPrizesEvent = 1124;
    public const uint PurchaseRoomAdEvent = 3051;
    public const uint PickUpBotEvent = 1037;
    public const uint GetOffersEvent = 2858;
    public const uint GetHabboGroupBadgesEvent = 1847;
    public const uint GetUserTagsEvent = 65382;
    public const uint GetGameAchievementsEvent = 65475;
    public const uint GetCatalogRoomPromotionEvent = 3894;
    public const uint MoveAvatarEvent = 1859;
    public const uint SaveBrandingItemEvent = 2955;
    public const uint SetAreaHideEvent = 3831;
    public const uint SaveEnforcedCategorySettingsEvent = 3740;
    public const uint RespectPetEvent = 958;
    public const uint GetMarketplaceCanMakeOfferEvent = 3684;
    public const uint UpdateMagicTileAdjacentEvent = 65449;
    public const uint UpdateMagicTileEvent = 1309;
    public const uint GetStickyNoteEvent = 210;
    public const uint IgnoreUserEvent = 1483;
    public const uint BanUserEvent = 3500;
    public const uint UpdateForumSettingsEvent = 307;
    public const uint GetRoomRightsEvent = 706;
    public const uint SendMsgEvent = 831;
    public const uint CloseTicketEvent = 3910;

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
    public const uint GetHabboClubExtendOfferEvent = 2261;
    public const uint PurchaseBasicMembershipExtensionEvent = 2822;
    public const uint PurchaseVipMembershipExtensionEvent = 1597;
    public const uint SelectClubGiftEvent = 3860;


    //recycler
    public const uint GetRecyclerStatusEvent = 3532;
    public const uint RecyclerRecycleEvent = 3093;


    //FriendList
    //public const uint FriendFurniConfirmLockEvent =;
    //public const uint VisitUserEvent =;

    //crafting
    public const uint CraftEvent = 2984;
    //public const uint CraftSecretEvent =;
    public const uint GetCraftableProductsEvent = 136;
    public const uint GetCraftingRecipeEvent = 809;
    //public const uint GetCraftingRecipesAvailableEvent =;

    // SnowStorm (AIR Game2 payloads); internal IDs match Volt's wire IDs.
    public const uint Game2CheckGameDirectoryStatusEvent = 822;
    public const uint Game2GetAccountGameStatusEvent = 2949;
    public const uint Game2QuickJoinEvent = 6012;
    public const uint Game2LeaveLobbyEvent = 6013;
    public const uint Game2LoadStageReadyEvent = 2914;
    public const uint Game2ExitGameEvent = 3482;
    public const uint Game2GameChatEvent = 584;
    public const uint Game2PlayAgainEvent = 2552;
    public const uint Game2SetUserMoveTargetEvent = 6003;
    public const uint Game2ThrowSnowballAtPositionEvent = 6004;
    public const uint Game2ThrowSnowballAtHumanEvent = 6005;
    public const uint Game2MakeSnowballEvent = 6006;
    public const uint Game2RequestFullStatusUpdateEvent = 2893;
    public const uint Game2GetTotalLeaderboardEvent = 6027;
    public const uint Game2GetFriendsLeaderboardEvent = 6028;
    public const uint Game2GetWeeklyFriendsLeaderboardEvent = 1163;
    public const uint Game2GetTotalGroupLeaderboardEvent = 27;
    public const uint Game2GetWeeklyGroupLeaderboardEvent = 1936;
    public const uint GetSnowWarGameTokensOfferEvent = 3976;
    public const uint PurchaseSnowWarGameTokensOfferEvent = 1212;




    public const uint TriggerHabbiconEvent = 1339;
    public const uint GetHabbiconShopDataEvent = 1846;
    public const uint GetHabbiconInfoEvent = 559;
    public const uint BuyHabbiconEvent = 62;
    public const uint BuyHabbiconCollectionEvent = 3516;
    public const uint ClaimHabbiconEvent = 3947;
    public const uint FavoriteHabbiconEvent = 1963;
    public const uint UnfavoriteHabbiconEvent = 2256;
    public const uint UnseenResetCategoryEvent = 65428;
    public const uint UnseenResetItemsEvent = 65459;
    public const uint SendMessengerMessageEvent = 4902;
    // Camera internal IDs; wire IDs are revision-specific.
    public const uint InitCameraEvent = 1270;
    public const uint PhotoCompetitionEvent = 90;
    public const uint PublishPhotoEvent = 1800;
    public const uint PurchasePhotoEvent = 3265;
    public const uint RenderRoomEvent = 2230;
    public const uint RenderRoomThumbnailEvent = 3337;
    public const uint GetRewardTracksEvent = 9450;
    public const uint ClaimRewardTrackPrizeEvent = 853;
    public const uint PurchaseRewardTrackPremiumEvent = 3850;
    // Daily tasks, AIR WIN63-202610151745-318700621 (quest/dailytasks/DailyTasksController.as).
    public const uint GetDailyTasksEvent = 4100; // package_24.class_1820
    public const uint ClaimDailyTaskEvent = 4101; // package_24.class_2568
    public const uint GetQuizQuestionsEvent = 3454;
    public const uint PostQuizAnswersEvent = 2101;
    // Room music
    public const uint AddJukeboxDiskEvent = 920;
    public const uint RemoveJukeboxDiskEvent = 347;
    public const uint GetJukeboxPlaylistEvent = 2300;
    public const uint GetSoundMachinePlaylistEvent = 1182;
    public const uint GetUserSongDisksEvent = 1945;
    public const uint GetNowPlayingEvent = 2646;
    public const uint GetOfficialSongIdEvent = 3042;
    public const uint WiredVariableInspectionRequestEvent = 10112;
    public const uint WiredUserVariableUpdate64Event = 10110;
    public const uint WiredUserVariableManage64Event = 10111;
    public const uint WiredUserVariablesRequest64Event = 10113;
    public const uint WiredVariableHoldersRequest64Event = 10114;
    public const uint WiredVariableHoldersPage64Event = 10115;
    // Wired chests and wired trades; see reviews/chest-protocol-map.md.
    public const uint ChestOpenEvent = 1606; // package_95.class_2556
    public const uint ChestCloseEvent = 3929; // package_95.class_2991
    public const uint ChestStartDepositEvent = 1850; // package_95.class_3680
    public const uint ChestWithdrawAllEvent = 1344; // package_95.class_3893
    public const uint ChestWithdrawFurniEvent = 3782; // package_95.class_2400
    public const uint ChestWithdrawCoinsEvent = 2836; // package_95.class_3317
    public const uint ChestSaveOptionsEvent = 2622; // package_95.class_3287
    public const uint ChestSavePreferencesEvent = 782; // package_95.class_3106
    public const uint ChestSaveNotificationsEvent = 1762; // package_95.class_2759
    public const uint WiredChestOfferItemsEvent = 3890; // package_186.class_2968
    public const uint WiredChestAcceptEvent = 1683; // package_186.class_3406
    public const uint WiredChestCancelEvent = 1711; // package_186.class_3271
    public const uint ChestUpgradeEvent = 1795; // package_95.class_3733
    public const uint WiredChestLockEvent = 414; // package_95.class_3520

    // October wired boxes (r88).
    public const uint WiredWebApiKeyRequestEvent = 3915; // class_2630
    public const uint WiredVariableShareSynchronizeEvent = 397; // class_3060
    public const uint WiredVariableLeaderboardSubscribeEvent = 1708; // package_221.class_3719
    public const uint SaveWiredLabelTextEvent = 1736; // class_2809
    public const uint SaveWiredLabelSettingsEvent = 951; // class_3231
    public const uint WiredLinkRequestConfigEvent = 3661; // package_129.class_2728
    public const uint WiredLinkSaveSelectionEvent = 296; // package_129.class_2545
    public const uint WiredLinkCreateNetworkEvent = 1297; // package_129.class_3727
    public const uint WiredLinkRenameNetworkEvent = 702; // package_129.class_3853
    public const uint WiredLinkDeleteNetworkEvent = 1728; // package_129.class_3634
    public const uint WiredLinkGetConnectionsEvent = 3745; // package_129.class_3536
    public const uint GetWiredContractContentsEvent = 1764; // package_193.class_3900
    public const uint UpdateWiredContractEvent = 1005; // package_193.class_3040
    // Wired monitor (wiredmenu composers); the monitor tab sends both requests together (WiredMenuMonitorTab.requestData).
    public const uint GetWiredRoomStatsEvent = 2525; // wiredmenu.class_3345
    public const uint GetWiredErrorLogsEvent = 3287; // wiredmenu.class_2587
    public const uint ClearWiredErrorLogsEvent = 2374; // wiredmenu.class_3251
    // The inspection tab's value edit, create and delete (WiredMenuInspectionTab.as:193, 249, 265).
    public const uint WiredSetObjectVariableValueEvent = 3190; // wiredmenu.class_3782
    // The inspection tab's read of one object's variables (WiredMenuInspectionTab.requestVariablesForObject).
    public const uint WiredGetVariablesForObjectEvent = 825; // wiredmenu.class_3018

    // Free product claims (cursed dragon adoption) and the new-feature promo expiry, AIR WIN63-202610151745-318700621.
    public const uint HasClaimedProductEvent = 2014; // package_143.class_3538
    public const uint ClaimProductEvent = 3149; // package_143.class_2610
    public const uint GetSecondsUntilEvent = 359; // package_146.class_3739
}
