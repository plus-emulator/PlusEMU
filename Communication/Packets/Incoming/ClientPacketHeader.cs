namespace Plus.Communication.Packets.Incoming;

public static class ClientPacketHeader
{
    // Handshake
    public const uint InitDiffieHandshakeEvent = 3799;
    public const uint CompleteDiffieHandshakeEvent = 3072;
    public const uint UniqueIdEvent = 483;
    public const uint SSOTicketEvent = 1623;
    public const uint InfoRetrieveEvent = 312;

    // Avatar
    public const uint CheckUserNameEvent = 2155;
    public const uint ChangeUserNameEvent = 1075;
    public const uint GetWardrobeEvent = 1796;
    public const uint SaveWardrobeOutfitEvent = 2775;

    //Preferences
    public const uint SetChatStylePreferenceEvent = 3002;
    public const uint SetChatPreferenceEvent = 65476;

    // Catalog
    public const uint GetCatalogIndexWithDiscountEvent = 3226; //1294
    public const uint GetCatalogPageEvent = 1972;
    public const uint GetProductOfferEvent = 336;
    public const uint GetKickbackInfoEvent = 84;
    public const uint GetClubGiftInfoEvent = 3091;
    public const uint PurchaseFromCatalogEvent = 2860;
    public const uint PurchaseFromCatalogAsGiftEvent = 1950;
    public const uint GetBundleDiscountRulesetEvent = 2529;

    // Polls
    public const uint PollStartEvent = 1069;
    public const uint PollRejectEvent = 2093;
    public const uint PollAnswerEvent = 2007;

    // Navigator

    // Messenger
    public const uint GetFriendRequestsEvent = 3080;

    // Quests
    public const uint GetQuestListEvent = 2025;
    public const uint StartQuestEvent = 1139;
    public const uint GetCurrentQuestEvent = 2148;
    public const uint CancelQuestEvent = 2022;

    // Room Avatar
    public const uint ActionEvent = 2225;
    public const uint ApplySignEvent = 2444;
    public const uint DanceEvent = 883;
    public const uint SitEvent = 2136;
    public const uint ChangeMottoEvent = 2901;
    public const uint LookToEvent = 981;
    public const uint DropHandItemEvent = 3584;

    // Room Connection
    public const uint OpenFlatConnectionEvent = 2377;
    public const uint GoToFlatEvent = 65442;

    // Room Chat
    public const uint ChatEvent = 1838;
    public const uint ShoutEvent = 2054;
    public const uint WhisperEvent = 286;

    // Room Engine

    // Room Settings

    // Room Action

    // Users
    public const uint GetIgnoredUsersEvent = 2986;

    // Moderation
    public const uint OpenHelpToolEvent = 3920;
    public const uint CallForHelpPendingCallsDeletedEvent = 485;
    public const uint ModeratorActionEvent = 474;
    public const uint ModerationMsgEvent = 410;
    public const uint ModerationMuteEvent = 1358;
    public const uint ModerationTradeLockEvent = 3931;
    public const uint GetModeratorUserRoomVisitsEvent = 3379;
    public const uint ModerationKickEvent = 3370;
    public const uint GetModeratorRoomInfoEvent = 2419;
    public const uint GetModeratorUserInfoEvent = 2342;
    public const uint GetModeratorRoomChatlogEvent = 1999;
    public const uint ModerateRoomEvent = 1083;
    public const uint GetModeratorUserChatlogEvent = 1320;
    public const uint GetModeratorTicketChatlogsEvent = 1231;
    public const uint ModerationCautionEvent = 2681;
    public const uint ModerationBanEvent = 679;
    public const uint SubmitNewTicketEvent = 3637;
    public const uint CloseIssueDefaultActionEvent = 3781;

    // Inventory
    public const uint GetCreditsInfoEvent = 1749;
    public const uint GetAchievementsEvent = 707;
    public const uint GetBadgesEvent = 2375;
    public const uint RequestFurniInventoryEvent = 2536;
    public const uint SetActivatedBadgesEvent = 3744;
    public const uint AvatarEffectActivatedEvent = 1469;
    public const uint AvatarEffectSelectedEvent = 2323;

    public const uint InitTradeEvent = 1344;
    public const uint TradingCancelConfirmEvent = 3456;
    public const uint TradingModifyEvent = 1691;
    public const uint TradingOfferItemEvent = 3133;
    public const uint TradingCancelEvent = 1148;
    public const uint TradingConfirmEvent = 2246;
    public const uint TradingOfferItemsEvent = 1538;
    public const uint TradingRemoveItemEvent = 301;
    public const uint TradingAcceptEvent = 1515;

    // Register
    public const uint UpdateFigureDataEvent = 3901;

    // Groups
    public const uint GetBadgeEditorPartsEvent = 2502;
    public const uint GetGroupCreationWindowEvent = 1255;
    public const uint GetGroupFurniSettingsEvent = 2856;
    public const uint DeclineGroupMembershipEvent = 3824;
    public const uint JoinGroupEvent = 2109;
    public const uint UpdateGroupColoursEvent = 3870;
    public const uint SetGroupFavouriteEvent = 43;
    public const uint GetGroupMembersEvent = 3927;

    // Group Forums
    public const uint PostGroupContentEvent = 1372;
    public const uint GetForumStatsEvent = 1711;
    public const uint GetForumThreadEvent = 379;
    public const uint UpdateForumReadMarkersEvent = 1533;
    public const uint GetForumsUnreadCountEvent = 1491;

    // Sound


    // Ambassador

    public const uint AmbassadorAlertEvent = 3032;

    public const uint RemoveMyRightsEvent = 680;
    public const uint GiveHandItemEvent = 45;
    public const uint GoToHotelViewEvent = 1780;
    public const uint GetRoomFilterListEvent = 980;
    public const uint GetPromoArticlesEvent = 3129;
    public const uint ModifyWhoCanRideHorseEvent = 429;
    public const uint RemoveFriendEvent = 612;
    public const uint RefreshCampaignEvent = 2523;
    public const uint AcceptFriendEvent = 2483;
    public const uint YouTubeVideoInformationEvent = 3734;
    public const uint FollowFriendEvent = 3807;
    public const uint SaveBotActionEvent = 3272;
    public const uint LetUserInEvent = 1117;
    public const uint GetMarketplaceItemStatsEvent = 3067;
    public const uint GetSellablePetPalettesEvent = 1830;
    public const uint OpenCampaignCalendarDoorAsStaffEvent = 2578;
    public const uint SetUIFlagsEvent = 270;
    public const uint DeleteRoomEvent = 1313;
    public const uint SetSoundSettingsEvent = 1548;
    public const uint InitializeGameCenterEvent = 65468;
    public const uint RedeemOfferCreditsEvent = 3420;
    public const uint FriendListUpdateEvent = 2388;
    public const uint FriendFurniConfirmLockEvent = 3629;
    public const uint UseHabboWheelEvent = 732;
    public const uint SaveRoomSettingsEvent = 956;
    public const uint ToggleMoodlightEvent = 3450;
    public const uint GetDailyQuestEvent = 3315;
    public const uint SetMannequinNameEvent = 2495;
    public const uint OneWayGateEvent = 269;
    public const uint EventTrackerEvent = 65429;
    public const uint GetOccupiedTilesEvent = 2998;
    public const uint PickUpPetEvent = 1426;
    public const uint GetPetInventoryEvent = 2822;
    public const uint GetRoomEntryTileEvent = 3665;
    public const uint GetOwnOffersEvent = 2188;
    public const uint CheckPetNameEvent = 3610;
    public const uint SetUserFocusPreferenceEvent = 1303;
    public const uint SubmitBullyReportEvent = 3073;
    public const uint RemoveRightsEvent = 351;
    public const uint MakeOfferEvent = 1010;
    public const uint KickUserEvent = 2517;
    public const uint GetRoomSettingsEvent = 53;
    public const uint GetThreadsListDataEvent = 83;
    public const uint GetForumUserProfileEvent = 510;
    public const uint OpenWiredEvent = 1438;
    public const uint WiredMonitorRequestEvent = 10021;
    public const uint WiredRoomLogsPageEvent = 324;
    public const uint WiredRoomSettingsRequestEvent = 10022;
    public const uint WiredRoomSettingsSaveEvent = 10023;
    public const uint WiredMenuPermissionsSaveEvent = 65465;
    public const uint ClickFurniEvent = 80;
    public const uint ClickUserEvent = 10020;
    public const uint WiredUserVariablesRequestEvent = 10024;
    public const uint WiredUserVariableUpdateEvent = 10025;
    public const uint WiredUserVariableManageEvent = 10026;
    public const uint WiredVariableHoldersPageEvent = 65307;
    public const uint WiredVariableHoldersRequestEvent = 65441;
    public const uint WiredVariableHashesEvent = 3150;
    public const uint WiredAllVariablesRequestEvent = 1070;
    public const uint SaveWiredEffectConfigEvent = 799;
    public const uint SaveWiredSelectorConfigEvent = 1758;
    public const uint SaveWiredAddonConfigEvent = 3371;
    public const uint SaveWiredVariableConfigEvent = 218;
    public const uint GetRoomEntryDataEvent = 65460;
    public const uint JoinQueueEvent = 65471;
    public const uint CanCreateRoomEvent = 924;
    public const uint SetTonerEvent = 2682;
    public const uint SaveWiredTriggerConfigEvent = 3842;
    public const uint PlaceBotEvent = 1807;
    public const uint MoveEntityInFlatEvent = 1528;
    public const uint GetRelationshipsEvent = 2749;
    public const uint SetMessengerInviteStatusEvent = 169;
    public const uint UseFurnitureEvent = 2002;
    public const uint GetUserFlatCatsEvent = 3845;
    public const uint AssignRightsEvent = 3696;
    public const uint GetRoomBannedUsersEvent = 1087;
    public const uint ReleaseTicketEvent = 431;
    public const uint OpenPlayerProfileEvent = 565;
    public const uint GetSanctionStatusEvent = 65447;
    public const uint CreditFurniRedeemEvent = 3651;
    public const uint DisconnectEvent = 2585;
    public const uint PickupObjectEvent = 58;
    public const uint FindRandomFriendingRoomEvent = 2574;
    public const uint UseSellableClothingEvent = 125;
    public const uint MoveObjectEvent = 1054;
    public const uint GetFurnitureAliasesEvent = 492;
    public const uint TakeAdminRightsEvent = 1513;
    public const uint ModifyRoomFilterListEvent = 2758;
    public const uint MoodlightUpdateEvent = 2209;
    public const uint GetPetTrainingPanelEvent = 252;
    public const uint GetSongInfoEvent = 1800;
    public const uint UseWallItemEvent = 2994;
    public const uint GetTalentTrackEvent = 3496;
    public const uint GiveAdminRightsEvent = 1443;
    public const uint GetCatalogIndexEvent = 56;
    public const uint SendBullyReportEvent = 1724;
    public const uint CancelOfferEvent = 938;
    public const uint SaveWiredConditionConfigEvent = 2794;
    public const uint RedeemVoucherEvent = 2728;
    public const uint ThrowDiceEvent = 1882;
    public const uint CraftSecretEvent = 2933;
    public const uint GetGameListEvent = 65391;
    public const uint SetRelationshipEvent = 1366;
    public const uint RequestFriendEvent = 3668;
    public const uint MemoryPerformanceEvent = 1556;
    public const uint ToggleYouTubeVideoEvent = 3911;
    public const uint SetMannequinFigureEvent = 3064;
    public const uint GetEventCategoriesEvent = 1899;
    public const uint DeleteGroupThreadEvent = 319;
    public const uint PurchaseGroupEvent = 538;
    public const uint MessengerInitEvent = 249;
    public const uint CancelTypingEvent = 2239;
    public const uint GetMoodlightConfigEvent = 1204;
    public const uint GetGroupInfoEvent = 216;
    public const uint CreateFlatEvent = 1129;
    public const uint LatencyPingRequestEvent = 1768;
    public const uint GetSelectedBadgesEvent = 154;
    public const uint AddStickyNoteEvent = 1633;
    public const uint RideHorseEvent = 647;
    public const uint InitializeNewNavigatorEvent = 2546;
    public const uint GetForumsListDataEvent = 3033;
    public const uint ToggleMuteToolEvent = 1773;
    public const uint UpdateGroupIdentityEvent = 1872;
    public const uint UpdateStickyNoteEvent = 210;
    public const uint UnbanUserFromRoomEvent = 13;
    public const uint UnignoreUserEvent = 2333;
    public const uint OpenGiftEvent = 3313;
    public const uint ApplyDecorationEvent = 503;
    public const uint GetRecipeConfigEvent = 2428; //3654
    public const uint ScrGetUserInfoEvent = 174;
    public const uint RemoveGroupMemberEvent = 3888;
    public const uint ConfirmRemoveGroupMemberEvent = 3580;
    public const uint DiceOffEvent = 3682;
    public const uint YouTubeGetNextVideo = 3532;
    public const uint RemoveFavouriteRoomEvent = 717;
    public const uint RespectUserEvent = 3520;
    public const uint AddFavouriteRoomEvent = 633;
    public const uint DeclineFriendEvent = 1499;
    public const uint StartTypingEvent = 729;
    public const uint GetGroupFurniConfigEvent = 1179;
    public const uint SendRoomInviteEvent = 733;
    public const uint RemoveAllRightsEvent = 2735;
    public const uint GetYouTubeTelevisionEvent = 748;
    public const uint FindNewFriendsEvent = 3250;
    public const uint GetPromotableRoomsEvent = 1395;
    public const uint GetBotInventoryEvent = 3476;
    public const uint GetRentableSpaceEvent = 693;
    public const uint OpenBotActionEvent = 1363;
    public const uint OpenCampaignCalendarDoorEvent = 2631;
    public const uint DeleteGroupPostEvent = 1752;
    public const uint UpdateGroupBadgeEvent = 3938;
    public const uint PlaceObjectEvent = 2821;
    public const uint RemoveGroupFavouriteEvent = 3461;
    public const uint UpdateNavigatorSettingsEvent = 3285;
    public const uint CheckGnomeNameEvent = 3241;
    public const uint NavigatorSearchEvent = 318;
    public const uint GetPetInformationEvent = 2923;
    public const uint GetGuestRoomEvent = 2234;
    public const uint UpdateThreadEvent = 136;
    public const uint AcceptGroupMembershipEvent = 548;
    public const uint GetMarketplaceConfigurationEvent = 3346;
    public const uint Game2GetWeeklyLeaderboardEvent = 3638;
    public const uint BuyOfferEvent = 3858;
    public const uint RemoveSaddleFromHorseEvent = 1946;
    public const uint GiveRoomScoreEvent = 3215;
    public const uint GetHabboClubWindowEvent = 1042;
    public const uint DeleteStickyNoteEvent = 1460;
    public const uint MuteUserEvent = 3007;
    public const uint ApplyHorseEffectEvent = 690;
    public const uint ClientHelloEvent = 4000; //4000
    public const uint OnBullyClickEvent = 65455;
    public const uint HabboSearchEvent = 1592;
    public const uint PickTicketEvent = 3113;
    public const uint GetGiftWrappingConfigurationEvent = 2609;
    public const uint GetCraftingRecipesAvailableEvent = 2194;
    public const uint GetThreadDataEvent = 3818;
    public const uint ManageGroupEvent = 1629;
    public const uint PlacePetEvent = 304;
    public const uint MovePetEvent = 2281;
    public const uint EditRoomPromotionEvent = 2881;
    public const uint UpdateFloorPropertiesEvent = 867;
    public const uint MoveWallItemEvent = 491;
    public const uint VersionCheckEvent = 155;
    public const uint PongEvent = 3743;
    public const uint DeleteGroupEvent = 3600;
    public const uint UpdateGroupSettingsEvent = 669;
    public const uint GetRecyclerPrizesEvent = 1526;
    public const uint PurchaseRoomAdEvent = 589;
    public const uint PickUpBotEvent = 433;
    public const uint GetOffersEvent = 2813;
    public const uint GetHabboGroupBadgesEvent = 2934;
    public const uint GetUserTagsEvent = 65382;
    public const uint GetGameAchievementsEvent = 65475;
    public const uint GetCatalogRoomPromotionEvent = 3390;
    public const uint MoveAvatarEvent = 2102;
    public const uint SaveBrandingItemEvent = 181;
    public const uint SaveEnforcedCategorySettingsEvent = 722;
    public const uint RespectPetEvent = 3342;
    public const uint GetMarketplaceCanMakeOfferEvent = 3097;
    public const uint UpdateMagicTileAdjacentEvent = 65449;
    public const uint UpdateMagicTileEvent = 525;
    public const uint GetStickyNoteEvent = 33;
    public const uint IgnoreUserEvent = 2400;
    public const uint BanUserEvent = 98;
    public const uint UpdateForumSettingsEvent = 2104;
    public const uint GetRoomRightsEvent = 211;
    public const uint SendMsgEvent = 2180;
    public const uint CloseTicketEvent = 239;

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
    //public const uint GetBonusRareInfoEvent =;
    //public const uint GetCatalogPageExpirationEvent =;
    //public const uint GetCatalogPageWithEarliestExpiryEvent =;
    //public const uint GetDirectClubBuyAvailableEvent =;
    //public const uint GetHabboBasicMembershipExtendOfferEvent =;
    //public const uint GetLimitedOfferAppearingNextEvent =;
    public const uint GetHabboClubExtendOfferEvent = 1926;
    //public const uint GetIsOfferGiftableEvent =;
    //public const uint GetNextTargetedOfferEvent =;
    //public const uint GetSeasonalCalendarDailyOfferEvent =;
    //public const uint MarkCatalogNewAdditionsPageOpenedEvent =;
    public const uint PurchaseBasicMembershipExtensionEvent = 3881;
    //public const uint PurchaseTargetedOfferEvent =;
    public const uint PurchaseVipMembershipExtensionEvent = 907;
    public const uint SelectClubGiftEvent = 2524;
    //public const uint SetTargetedOfferStateEvent =;
    //public const uint ShopTargetedOfferViewedEvent =;


    //recycler
    public const uint GetRecyclerStatusEvent = 293;
    public const uint RecyclerRecycleEvent = 3164;


    //FriendList
    //public const uint FriendFurniConfirmLockEvent =;
    //public const uint SetRelationshipStatusEvent =;
    //public const uint VisitUserEvent =;

    //competition
    //public const uint ForwardToACompetitionRoomEvent =;
    //public const uint ForwardToASubmittableRoomEvent =;
    //public const uint ForwardToRandomCompetitionRoomEvent =;
    //public const uint GetCurrentTimingCodeEvent =;
    //public const uint GetIsUserPartOfCompetitionEvent =;
    //public const uint GetSecondsUntilEvent =;
    //public const uint RoomCompetitionInitEvent =;
    //public const uint SubmitRoomToCompetitionEvent =;
    //public const uint VoteForRoomEvent =;

    //crafting
    public const uint CraftEvent = 2100;
    //public const uint CraftSecretEvent =;
    public const uint GetCraftableProductsEvent = 822;
    public const uint GetCraftingRecipeEvent = 1531;
    //public const uint GetCraftingRecipesAvailableEvent =;

    // SnowStorm (AIR Game2 payloads); internal IDs match Volt's wire IDs.
    public const uint Game2CheckGameDirectoryStatusEvent = 2064;
    public const uint Game2GetAccountGameStatusEvent = 2040;
    public const uint Game2QuickJoinEvent = 6012;
    public const uint Game2LeaveLobbyEvent = 6013;
    public const uint Game2LoadStageReadyEvent = 832;
    public const uint Game2ExitGameEvent = 2843;
    public const uint Game2GameChatEvent = 1656;
    public const uint Game2PlayAgainEvent = 3748;
    public const uint Game2SetUserMoveTargetEvent = 6003;
    public const uint Game2ThrowSnowballAtPositionEvent = 6004;
    public const uint Game2ThrowSnowballAtHumanEvent = 6005;
    public const uint Game2MakeSnowballEvent = 6006;
    public const uint Game2RequestFullStatusUpdateEvent = 1288;
    public const uint Game2GetTotalLeaderboardEvent = 6027;
    public const uint Game2GetFriendsLeaderboardEvent = 6028;
    public const uint Game2GetWeeklyFriendsLeaderboardEvent = 3228;
    public const uint Game2GetTotalGroupLeaderboardEvent = 2533;
    public const uint Game2GetWeeklyGroupLeaderboardEvent = 1977;
    public const uint GetSnowWarGameTokensOfferEvent = 2138;
    public const uint PurchaseSnowWarGameTokensOfferEvent = 3625;




    public const uint TriggerHabbiconEvent = 3892;
    public const uint GetHabbiconShopDataEvent = 206;
    public const uint GetHabbiconInfoEvent = 1552;
    public const uint BuyHabbiconEvent = 2084;
    public const uint BuyHabbiconCollectionEvent = 315;
    public const uint ClaimHabbiconEvent = 3477;
    public const uint FavoriteHabbiconEvent = 1583;
    public const uint UnfavoriteHabbiconEvent = 1331;
    public const uint UnseenResetCategoryEvent = 65428;
    public const uint UnseenResetItemsEvent = 65459;
    public const uint SendMessengerMessageEvent = 4902;
    // Camera internal IDs; wire IDs are revision-specific.
    public const uint InitCameraEvent = 949;
    public const uint PhotoCompetitionEvent = 2911;
    public const uint PublishPhotoEvent = 3506;
    public const uint PurchasePhotoEvent = 3130;
    public const uint RenderRoomEvent = 129;
    public const uint RenderRoomThumbnailEvent = 3429;
    public const uint GetRewardTracksEvent = 9450;
    public const uint ClaimRewardTrackPrizeEvent = 1784;
    public const uint PurchaseRewardTrackPremiumEvent = 3162;
    public const uint GetQuizQuestionsEvent = 2270;
    public const uint PostQuizAnswersEvent = 2158;
    // Room music
    public const uint AddJukeboxDiskEvent = 1795;
    public const uint RemoveJukeboxDiskEvent = 2630;
    public const uint GetJukeboxPlaylistEvent = 3751;
    public const uint GetSoundMachinePlaylistEvent = 359;
    public const uint GetUserSongDisksEvent = 893;
    public const uint GetNowPlayingEvent = 778;
    public const uint GetOfficialSongIdEvent = 1428;
    public const uint WiredVariableInspectionRequestEvent = 10112;
    public const uint WiredUserVariableUpdate64Event = 10110;
    public const uint WiredUserVariableManage64Event = 10111;
    public const uint WiredUserVariablesRequest64Event = 10113;
    public const uint WiredVariableHoldersRequest64Event = 10114;
    public const uint WiredVariableHoldersPage64Event = 10115;
    public const uint ChestOpenEvent = 9327;
    public const uint ChestCloseEvent = 9339;
    public const uint ChestStartDepositEvent = 9324;
    public const uint ChestDepositInventoryItemEvent = 9325;
    public const uint ChestWithdrawAllEvent = 9326;
    public const uint ChestWithdrawFurniEvent = 9320;
    public const uint ChestWithdrawCoinsEvent = 9314;
    public const uint ChestDepositCoinsEvent = 9313;
    public const uint ChestSaveOptionsEvent = 9338;
    public const uint ChestSavePreferencesEvent = 9315;
    public const uint ChestEnableWiredEvent = 9345;
    public const uint ChestSaveNotificationsEvent = 9316;
    public const uint WiredChestOfferItemsEvent = 9335;
    public const uint WiredChestAcceptEvent = 9336;
    public const uint WiredChestCancelEvent = 9337;
    public const uint ChestUpgradeEvent = 9317;
    public const uint WiredChestLockEvent = 9329;
}
