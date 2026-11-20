namespace Plus.Communication.Packets.Incoming;

public static class ClientPacketHeader
{
    // Handshake
    public const uint InitDiffieHandshakeEvent = 1505;
    public const uint CompleteDiffieHandshakeEvent = 191;
    public const uint UniqueIdEvent = 3365;
    public const uint SSOTicketEvent = 2873;
    public const uint InfoRetrieveEvent = 1437;

    // Avatar
    public const uint CheckUserNameEvent = 1804;
    public const uint ChangeUserNameEvent = 3258;
    public const uint GetWardrobeEvent = 1244;
    public const uint SaveWardrobeOutfitEvent = 1867;
    public const uint GetUserNftWardrobeEvent = 1426;
    public const uint GetSelectedNftWardrobeOutfitEvent = 1794;
    public const uint SaveUserNftWardrobeEvent = 1636;

    // Collectibles hub
    public const uint GetCollectibleWalletAddressesEvent = 2649;
    public const uint GetCollectorScoreEvent = 1555;
    public const uint GetNftCollectionsEvent = 540;
    public const uint ClaimNftCollectionBonusEvent = 3178;
    public const uint ClaimNftCollectionRewardEvent = 3799;
    public const uint GetNftStoreOffersEvent = 1223;
    public const uint PurchaseNftStoreOfferEvent = 159;
    public const uint GetCollectibleMintableItemTypesEvent = 715;
    public const uint GetCollectibleMintingEnabledEvent = 1857;
    public const uint MintCollectibleEvent = 2469;
    public const uint GetNftClaimsEvent = 3987;
    public const uint ClaimNftRewardsEvent = 949;

    //Preferences
    public const uint SetChatStylePreferenceEvent = 1594;
    public const uint SetChatDisplayPreferencesEvent = 2498;
    public const uint SetOnlineIndicatorPreferenceEvent = 2554;
    public const uint SetChatPreferenceEvent = 65476;

    // Catalog
    public const uint GetCatalogPageEvent = 3485;
    public const uint GetProductOfferEvent = 2316;
    public const uint GetKickbackInfoEvent = 1883;
    public const uint GetClubGiftInfoEvent = 391;
    public const uint PurchaseFromCatalogEvent = 964;
    public const uint PurchaseFromCatalogAsGiftEvent = 1902;
    public const uint GetBundleDiscountRulesetEvent = 728;

    // Polls
    public const uint PollStartEvent = 2916;
    public const uint PollRejectEvent = 2908;
    public const uint PollAnswerEvent = 917;

    // Navigator

    // Messenger
    public const uint GetFriendRequestsEvent = 2953;

    // Quests
    public const uint GetQuestListEvent = 3290;
    public const uint StartQuestEvent = 1776;
    public const uint GetCurrentQuestEvent = 3506;
    public const uint CancelQuestEvent = 1382;

    // Room Avatar
    public const uint ActionEvent = 1548;
    public const uint ApplySignEvent = 600;
    public const uint DanceEvent = 3402;
    public const uint SitEvent = 1657;
    public const uint ChangeMottoEvent = 2715;
    public const uint LookToEvent = 3181;
    public const uint DropHandItemEvent = 3016;

    // Room Connection
    public const uint OpenFlatConnectionEvent = 1312;
    public const uint GoToFlatEvent = 65442;

    // Room Chat
    public const uint ChatEvent = 563;
    public const uint ShoutEvent = 3964;
    public const uint WhisperEvent = 2774;

    // Room Engine

    // Room Settings

    // Room Action

    // Users
    public const uint GetIgnoredUsersEvent = 2013;

    // Moderation
    public const uint OpenHelpToolEvent = 3713;
    public const uint CallForHelpPendingCallsDeletedEvent = 3532;
    public const uint ModeratorActionEvent = 1767;
    public const uint ModerationMsgEvent = 1829;
    public const uint ModerationMuteEvent = 1854;
    public const uint ModerationTradeLockEvent = 225;
    public const uint GetModeratorUserRoomVisitsEvent = 2406;
    public const uint ModerationKickEvent = 2277;
    public const uint GetModeratorRoomInfoEvent = 319;
    public const uint GetModeratorUserInfoEvent = 2787;
    public const uint GetModeratorRoomChatlogEvent = 1573;
    public const uint ModerateRoomEvent = 3226;
    public const uint GetModeratorUserChatlogEvent = 744;
    public const uint GetModeratorTicketChatlogsEvent = 223;
    public const uint ModerationCautionEvent = 2099;
    public const uint ModerationBanEvent = 1553;
    public const uint SubmitNewTicketEvent = 126;
    public const uint CloseIssueDefaultActionEvent = 1591;

    // Inventory
    public const uint GetCreditsInfoEvent = 1473;
    public const uint GetAchievementsEvent = 323;
    public const uint GetBadgesEvent = 751;
    public const uint GetBadgeLeaderboardEvent = 2634;
    public const uint RequestFurniInventoryEvent = 2777;
    public const uint SetActivatedBadgesEvent = 1139;
    public const uint AvatarEffectActivatedEvent = 2977;
    public const uint AvatarEffectSelectedEvent = 3042;

    public const uint InitTradeEvent = 1123;
    public const uint TradingCancelConfirmEvent = 2656;
    public const uint TradingModifyEvent = 1489;
    public const uint TradingOfferItemEvent = 761;
    public const uint TradingCancelEvent = 2401;
    public const uint TradingConfirmEvent = 2555;
    public const uint TradingOfferItemsEvent = 423;
    public const uint TradingRemoveItemEvent = 58;
    public const uint TradingAcceptEvent = 739;

    // Register
    public const uint UpdateFigureDataEvent = 2387;

    // Groups
    public const uint GetBadgeEditorPartsEvent = 3180;
    public const uint GetGroupCreationWindowEvent = 2402;
    public const uint GetGroupFurniSettingsEvent = 3591;
    public const uint DeclineGroupMembershipEvent = 2470;
    public const uint JoinGroupEvent = 1368;
    public const uint UpdateGroupColoursEvent = 3450;
    public const uint SetGroupFavouriteEvent = 3283;
    public const uint GetGroupMembersEvent = 3410;

    // Group Forums
    public const uint PostGroupContentEvent = 937;
    public const uint GetForumStatsEvent = 3474;
    public const uint GetForumThreadEvent = 2301;
    public const uint UpdateForumReadMarkersEvent = 2267;
    public const uint GetForumsUnreadCountEvent = 2697;

    // Sound


    // Ambassador

    public const uint AmbassadorAlertEvent = 3979;

    public const uint RemoveMyRightsEvent = 421;
    public const uint GiveHandItemEvent = 1007;
    public const uint GoToHotelViewEvent = 368;
    public const uint GetRoomFilterListEvent = 2275;
    public const uint GetPromoArticlesEvent = 3033;
    public const uint ModifyWhoCanRideHorseEvent = 1012;
    public const uint TogglePetBreedingPermissionEvent = 3599;
    public const uint BreedPetsEvent = 2047;
    public const uint RemoveFriendEvent = 409;
    public const uint RefreshCampaignEvent = 3440;
    public const uint AcceptFriendEvent = 2333;
    public const uint YouTubeVideoInformationEvent = 65401; // The client has no composer for this id; 2488 is TryPhoneNumber
    public const uint FollowFriendEvent = 1124;
    public const uint SaveBotActionEvent = 3989;
    public const uint LetUserInEvent = 3009;
    public const uint GetMarketplaceItemStatsEvent = 2999;
    public const uint GetSellablePetPalettesEvent = 2382;
    public const uint OpenCampaignCalendarDoorAsStaffEvent = 3055;
    public const uint SetUIFlagsEvent = 132;
    public const uint DeleteRoomEvent = 3345;
    public const uint SetSoundSettingsEvent = 194;
    public const uint GetSoundSettingsEvent = 2887;
    public const uint InitializeGameCenterEvent = 65468;
    public const uint RedeemOfferCreditsEvent = 1098;
    public const uint FriendListUpdateEvent = 3421;
    public const uint FriendFurniConfirmLockEvent = 1778;
    public const uint UseHabboWheelEvent = 3021;
    public const uint SaveRoomSettingsEvent = 1645;
    public const uint ToggleMoodlightEvent = 3049;
    public const uint GetDailyQuestEvent = 3368;
    public const uint SetMannequinNameEvent = 3906;
    public const uint OneWayGateEvent = 1184;
    public const uint EventTrackerEvent = 65429;
    public const uint GetOccupiedTilesEvent = 1519;
    public const uint PickUpPetEvent = 2315;
    public const uint GetPetInventoryEvent = 1846;
    public const uint GetRoomEntryTileEvent = 3557;
    public const uint GetOwnOffersEvent = 3150;
    public const uint CheckPetNameEvent = 3422;
    public const uint SetUserFocusPreferenceEvent = 100;
    public const uint SubmitBullyReportEvent = 1354;
    public const uint RemoveRightsEvent = 1958;
    public const uint MakeOfferEvent = 810;
    public const uint KickUserEvent = 3608;
    public const uint GetRoomSettingsEvent = 647;
    public const uint GetThreadsListDataEvent = 1542;
    public const uint GetForumUserProfileEvent = 605;
    public const uint OpenWiredEvent = 2884;
    public const uint WiredMonitorRequestEvent = 10021;
    public const uint WiredRoomLogsPageEvent = 357;
    public const uint WiredRoomSettingsRequestEvent = 10022;
    public const uint WiredRoomSettingsSaveEvent = 10023;
    public const uint WiredGetRoomSettingsEvent = 547;
    public const uint WiredSetRoomSettingsEvent = 1300;
    public const uint WiredSetPreferencesEvent = 435;
    public const uint WiredMenuPermissionsSaveEvent = 65465;
    public const uint WiredRoomUpdateEvent = 1504;
    public const uint ClickFurniEvent = 3104;
    public const uint ClickUserEvent = 10020;
    public const uint WiredUserVariablesRequestEvent = 10024;
    public const uint WiredUserVariableUpdateEvent = 10025;
    public const uint WiredUserVariableManageEvent = 10026;
    public const uint WiredVariableHoldersPageEvent = 65307;
    public const uint WiredVariableHoldersRequestEvent = 65441;
    public const uint WiredVariableHashesEvent = 2186;
    public const uint WiredAllVariablesRequestEvent = 2376;
    public const uint SaveWiredEffectConfigEvent = 1799;
    public const uint ApplySnapshotEvent = 750;
    public const uint SaveWiredSelectorConfigEvent = 3303;
    public const uint SaveWiredAddonConfigEvent = 1481;
    public const uint SaveWiredVariableConfigEvent = 2171;
    public const uint GetRoomEntryDataEvent = 65460;
    public const uint JoinQueueEvent = 65471;
    public const uint CanCreateRoomEvent = 3352;
    public const uint SetTonerEvent = 2092;
    public const uint RecolorFurnitureEvent = 453;
    public const uint SaveWiredTriggerConfigEvent = 659;
    public const uint PlaceBotEvent = 1402;
    public const uint MoveEntityInFlatEvent = 2046;
    public const uint GetRelationshipsEvent = 692;
    public const uint SetMessengerInviteStatusEvent = 641;
    public const uint UseFurnitureEvent = 520;
    public const uint GetUserFlatCatsEvent = 2827;
    public const uint AssignRightsEvent = 3491;
    public const uint GetRoomBannedUsersEvent = 1150;
    public const uint ReleaseTicketEvent = 1961;
    public const uint OpenPlayerProfileEvent = 717;
    public const uint GetSanctionStatusEvent = 65447;
    public const uint CreditFurniRedeemEvent = 1396;
    public const uint DisconnectEvent = 2286;
    public const uint PickupObjectEvent = 437;
    public const uint FindRandomFriendingRoomEvent = 1199;
    public const uint UseSellableClothingEvent = 3873;
    public const uint MoveObjectEvent = 2497;
    public const uint GetFurnitureAliasesEvent = 3212;
    public const uint TakeAdminRightsEvent = 2074;
    public const uint ModifyRoomFilterListEvent = 3855;
    public const uint MoodlightUpdateEvent = 2308;
    public const uint GetPetTrainingPanelEvent = 43;
    public const uint GetSongInfoEvent = 510;
    public const uint UseWallItemEvent = 2270;
    public const uint GetTalentTrackEvent = 2935;
    public const uint GiveAdminRightsEvent = 1824;
    public const uint GetCatalogIndexEvent = 2566;
    public const uint BuildersClubPlaceRoomItemEvent = 3379;
    public const uint BuildersClubPlaceWallItemEvent = 3294;
    public const uint GetGuideReportingStatusEvent = 842;
    public const uint CancelOfferEvent = 1414;
    public const uint SaveWiredConditionConfigEvent = 1593;
    public const uint RedeemVoucherEvent = 753;
    public const uint ThrowDiceEvent = 1877;
    public const uint CraftSecretEvent = 1438;
    public const uint GetGameListEvent = 65391;
    public const uint SetRelationshipEvent = 2525;
    public const uint RequestFriendEvent = 1439;
    public const uint MemoryPerformanceEvent = 1252;
    public const uint ToggleYouTubeVideoEvent = 3877;
    public const uint SetMannequinFigureEvent = 2271;
    public const uint SetClothingChangeDataEvent = 3413;
    public const uint GetEventCategoriesEvent = 1678;
    public const uint DeleteGroupThreadEvent = 2746;
    public const uint PurchaseGroupEvent = 398;
    public const uint MessengerInitEvent = 886;
    public const uint CancelTypingEvent = 2984;
    public const uint GetMoodlightConfigEvent = 2393;
    public const uint GetGroupInfoEvent = 3815;
    public const uint CreateFlatEvent = 1126;
    public const uint LatencyPingRequestEvent = 2361;
    public const uint GetSelectedBadgesEvent = 1785;
    public const uint AddStickyNoteEvent = 3919;
    public const uint RideHorseEvent = 2161;
    public const uint InitializeNewNavigatorEvent = 618;
    public const uint GetForumsListDataEvent = 2272;
    public const uint ToggleMuteToolEvent = 304;
    public const uint UpdateGroupIdentityEvent = 1680;
    public const uint UpdateStickyNoteEvent = 3220;
    public const uint UnbanUserFromRoomEvent = 3690;
    public const uint UnignoreUserEvent = 3849;
    public const uint OpenGiftEvent = 814;
    public const uint ApplyDecorationEvent = 840;
    public const uint ScrGetUserInfoEvent = 2050;
    public const uint RemoveGroupMemberEvent = 1049;
    public const uint ConfirmRemoveGroupMemberEvent = 542;
    public const uint DiceOffEvent = 38;
    public const uint YouTubeGetNextVideo = 2563;
    public const uint RemoveFavouriteRoomEvent = 3672;
    public const uint RespectUserEvent = 2539;
    public const uint AddFavouriteRoomEvent = 267;
    public const uint DeclineFriendEvent = 3756;
    public const uint StartTypingEvent = 1745;
    public const uint GetGroupFurniConfigEvent = 3734;
    public const uint SendRoomInviteEvent = 34;
    public const uint RemoveAllRightsEvent = 1676;
    public const uint GetYouTubeTelevisionEvent = 3529;
    public const uint FindNewFriendsEvent = 1103;
    public const uint GetPromotableRoomsEvent = 1730;
    public const uint GetBotInventoryEvent = 2812;
    public const uint GetRentableSpaceEvent = 2477;
    public const uint OpenBotActionEvent = 3077;
    public const uint OpenCampaignCalendarDoorEvent = 487;
    public const uint DeleteGroupPostEvent = 3093;
    public const uint UpdateGroupBadgeEvent = 93;
    public const uint PlaceObjectEvent = 1910;
    public const uint RemoveGroupFavouriteEvent = 3076;
    public const uint UpdateNavigatorSettingsEvent = 3653;
    public const uint CheckGnomeNameEvent = 3048;
    public const uint NavigatorSearchEvent = 2345;
    public const uint GetPetInformationEvent = 1187;
    public const uint GetGuestRoomEvent = 2237;
    public const uint UpdateThreadEvent = 3560;
    public const uint AcceptGroupMembershipEvent = 2119;
    public const uint GetMarketplaceConfigurationEvent = 344;
    public const uint Game2GetWeeklyLeaderboardEvent = 2188;
    public const uint BuyOfferEvent = 581;
    public const uint RemoveSaddleFromHorseEvent = 1064;
    public const uint GiveRoomScoreEvent = 716;
    public const uint GetHabboClubWindowEvent = 1013;
    public const uint DeleteStickyNoteEvent = 1247;
    public const uint MuteUserEvent = 475;
    public const uint ApplyHorseEffectEvent = 3262;
    public const uint ClientHelloEvent = 4000; //4000
    public const uint OnBullyClickEvent = 65455;
    public const uint HabboSearchEvent = 1378;
    public const uint PickTicketEvent = 1497;
    public const uint GetGiftWrappingConfigurationEvent = 551;
    public const uint GetCraftingRecipesAvailableEvent = 1230;
    public const uint GetThreadDataEvent = 3222;
    public const uint ManageGroupEvent = 2650;
    public const uint PlacePetEvent = 1668;
    public const uint MovePetEvent = 3468;
    public const uint EditRoomPromotionEvent = 3298;
    public const uint UpdateFloorPropertiesEvent = 2996;
    public const uint MoveWallItemEvent = 3645;
    public const uint VersionCheckEvent = 2057;
    public const uint PongEvent = 3115;
    public const uint DeleteGroupEvent = 963;
    public const uint UpdateGroupSettingsEvent = 1916;
    public const uint GetRecyclerPrizesEvent = 987;
    public const uint PurchaseRoomAdEvent = 981;
    public const uint PickUpBotEvent = 3023;
    public const uint GetOffersEvent = 3616;
    public const uint GetHabboGroupBadgesEvent = 513;
    public const uint GetUserTagsEvent = 65382;
    public const uint GetGameAchievementsEvent = 65475;
    public const uint GetBonusRareInfoEvent = 2283;
    public const uint MoveAvatarEvent = 2912;
    public const uint SaveBrandingItemEvent = 2826;
    public const uint SetAreaHideEvent = 1802;
    public const uint SaveEnforcedCategorySettingsEvent = 1931;
    public const uint RespectPetEvent = 2403;
    public const uint GetMarketplaceCanMakeOfferEvent = 3054;
    public const uint UpdateMagicTileAdjacentEvent = 65449;
    public const uint UpdateMagicTileEvent = 3399;
    public const uint GetStickyNoteEvent = 1452;
    public const uint IgnoreUserEvent = 700;
    public const uint BanUserEvent = 1285;
    public const uint UpdateForumSettingsEvent = 216;
    public const uint GetRoomRightsEvent = 1814;
    public const uint SendMsgEvent = 2882;
    public const uint CloseTicketEvent = 1132;

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
    public const uint GetHabboClubExtendOfferEvent = 168;
    public const uint PurchaseBasicMembershipExtensionEvent = 3359;
    public const uint PurchaseVipMembershipExtensionEvent = 111;
    public const uint SelectClubGiftEvent = 1967;


    //recycler
    public const uint GetRecyclerStatusEvent = 293;
    public const uint RecyclerRecycleEvent = 3736;


    //FriendList
    //public const uint FriendFurniConfirmLockEvent =;
    //public const uint VisitUserEvent =;

    //crafting
    public const uint CraftEvent = 925;
    //public const uint CraftSecretEvent =;
    public const uint GetCraftableProductsEvent = 2728;
    public const uint GetCraftingRecipeEvent = 1460;
    //public const uint GetCraftingRecipesAvailableEvent =;

    // SnowStorm (AIR Game2).
    public const uint Game2CheckGameDirectoryStatusEvent = 2718;
    public const uint Game2GetAccountGameStatusEvent = 1374;
    public const uint Game2QuickJoinEvent = 3507;
    public const uint Game2LeaveLobbyEvent = 1047;
    public const uint Game2LoadStageReadyEvent = 1791;
    public const uint Game2ExitGameEvent = 2128;
    public const uint Game2GameChatEvent = 2481;
    public const uint Game2PlayAgainEvent = 1039;
    public const uint Game2SetUserMoveTargetEvent = 3930;
    public const uint Game2ThrowSnowballAtPositionEvent = 1815;
    public const uint Game2ThrowSnowballAtHumanEvent = 3618;
    public const uint Game2MakeSnowballEvent = 3780;
    public const uint Game2RequestFullStatusUpdateEvent = 3714;
    public const uint Game2GetTotalLeaderboardEvent = 1813;
    public const uint Game2GetFriendsLeaderboardEvent = 1165;
    public const uint Game2GetWeeklyFriendsLeaderboardEvent = 2353;
    public const uint Game2GetTotalGroupLeaderboardEvent = 2059;
    public const uint Game2GetWeeklyGroupLeaderboardEvent = 1193;
    public const uint GetSnowWarGameTokensOfferEvent = 1128;
    public const uint PurchaseSnowWarGameTokensOfferEvent = 666;




    public const uint TriggerHabbiconEvent = 3765;
    public const uint GetHabbiconShopDataEvent = 2132;
    public const uint GetHabbiconInfoEvent = 2592;
    public const uint BuyHabbiconEvent = 2453;
    public const uint BuyHabbiconCollectionEvent = 3870;
    public const uint ClaimHabbiconEvent = 2808;
    public const uint FavoriteHabbiconEvent = 2127;
    public const uint UnfavoriteHabbiconEvent = 2784;
    public const uint UnseenResetCategoryEvent = 65428;
    public const uint UnseenResetItemsEvent = 65459;
    public const uint SendMessengerMessageEvent = 4902;
    // Camera internal IDs; wire IDs are revision-specific.
    public const uint InitCameraEvent = 2149;
    public const uint PhotoCompetitionEvent = 1161;
    public const uint PublishPhotoEvent = 507;
    public const uint PurchasePhotoEvent = 1569;
    public const uint RenderRoomEvent = 3029;
    public const uint RenderRoomThumbnailEvent = 2168;
    public const uint GetRewardTracksEvent = 9450;
    public const uint ClaimRewardTrackPrizeEvent = 145;
    public const uint PurchaseRewardTrackPremiumEvent = 1818;
    // Daily tasks.
    public const uint GetDailyTasksEvent = 4100;
    public const uint ClaimDailyTaskEvent = 4101;
    public const uint GetQuizQuestionsEvent = 2369;
    public const uint PostQuizAnswersEvent = 2579;
    // Room music
    public const uint AddJukeboxDiskEvent = 2974;
    public const uint RemoveJukeboxDiskEvent = 609;
    public const uint GetJukeboxPlaylistEvent = 1947;
    public const uint GetSoundMachinePlaylistEvent = 808;
    public const uint GetUserSongDisksEvent = 3248;
    public const uint GetNowPlayingEvent = 1016;
    public const uint GetOfficialSongIdEvent = 3332;
    public const uint WiredVariableInspectionRequestEvent = 10112;
    public const uint WiredUserVariableUpdate64Event = 10110;
    public const uint WiredUserVariableManage64Event = 10111;
    public const uint WiredUserVariablesRequest64Event = 10113;
    public const uint WiredVariableHoldersRequest64Event = 10114;
    public const uint WiredVariableHoldersPage64Event = 10115;
    // Wired chests and wired trades; see reviews/chest-protocol-map.md.
    public const uint ChestOpenEvent = 2781;
    public const uint ChestCloseEvent = 349;
    public const uint ChestStartDepositEvent = 1372;
    public const uint ChestWithdrawAllEvent = 1997;
    public const uint ChestWithdrawFurniEvent = 713;
    public const uint ChestWithdrawCoinsEvent = 447;
    public const uint ChestSaveOptionsEvent = 2249;
    public const uint ChestSavePreferencesEvent = 3684;
    public const uint ChestSaveNotificationsEvent = 1754;
    public const uint WiredChestOfferItemsEvent = 339;
    public const uint WiredChestAcceptEvent = 2164;
    public const uint WiredChestCancelEvent = 742;
    public const uint ChestUpgradeEvent = 3620;
    public const uint WiredChestLockEvent = 663;
    public const uint WiredTransactionGetRoomLogsEvent = 2180;
    public const uint WiredTransactionGetChestLogsEvent = 35;

    // October wired boxes.
    public const uint WiredWebApiKeyRequestEvent = 2737;
    public const uint WiredVariableShareSynchronizeEvent = 3999;
    public const uint WiredVariableLeaderboardSubscribeEvent = 3027;
    public const uint SaveWiredLabelTextEvent = 857;
    public const uint SaveWiredLabelSettingsEvent = 3575;
    public const uint WiredLinkRequestConfigEvent = 1876;
    public const uint WiredLinkSaveSelectionEvent = 2438;
    public const uint WiredLinkCreateNetworkEvent = 50;
    public const uint WiredLinkRenameNetworkEvent = 570;
    public const uint WiredLinkDeleteNetworkEvent = 2217;
    public const uint WiredLinkGetConnectionsEvent = 620;
    public const uint GetWiredContractContentsEvent = 361;
    public const uint UpdateWiredContractEvent = 1431;
    // Wired monitor. The monitor tab sends both requests together.
    public const uint GetWiredRoomStatsEvent = 3451;
    public const uint GetWiredErrorLogsEvent = 3666;
    public const uint ClearWiredErrorLogsEvent = 1310;
    // The inspection tab's value edit, create and delete.
    public const uint WiredSetObjectVariableValueEvent = 454;
    // The inspection tab's read of one object's variables.
    public const uint WiredGetVariablesForObjectEvent = 1177;
    // The Variables tab's Highlight, Delete and Manage buttons.
    public const uint WiredGetAllVariableHoldersEvent = 2652;
    public const uint WiredDeleteAllVariableHoldersEvent = 2480;
    public const uint WiredGetVariableOwnersPageEvent = 496;
    public const uint WiredGetUserPermanentVariablesEvent = 2035;
    public const uint WiredSetUserPermanentVariableEvent = 627;

    // Free product claims (cursed dragon adoption) and the new-feature promo expiry.
    public const uint HasClaimedProductEvent = 2160;
    public const uint ClaimProductEvent = 2671;
    public const uint GetSecondsUntilEvent = 1066;
}
