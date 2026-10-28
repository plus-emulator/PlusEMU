namespace Plus.Communication.Packets.Incoming;

public static class ClientPacketHeader
{
    // Handshake
    public const uint InitDiffieHandshakeEvent = 998;
    public const uint CompleteDiffieHandshakeEvent = 2237;
    public const uint UniqueIdEvent = 2415;
    public const uint SSOTicketEvent = 3177;
    public const uint InfoRetrieveEvent = 3742;

    // Avatar
    public const uint CheckUserNameEvent = 3303;
    public const uint ChangeUserNameEvent = 17;
    public const uint GetWardrobeEvent = 873;
    public const uint SaveWardrobeOutfitEvent = 1085;
    public const uint GetUserNftWardrobeEvent = 3284;
    public const uint GetSelectedNftWardrobeOutfitEvent = 56;
    public const uint SaveUserNftWardrobeEvent = 489;

    // Collectibles hub
    public const uint GetCollectibleWalletAddressesEvent = 3913;
    public const uint GetCollectorScoreEvent = 2368;
    public const uint GetNftCollectionsEvent = 60;
    public const uint ClaimNftCollectionBonusEvent = 1653;
    public const uint ClaimNftCollectionRewardEvent = 3288;
    public const uint GetNftStoreOffersEvent = 3277;
    public const uint PurchaseNftStoreOfferEvent = 2688;
    public const uint GetCollectibleMintableItemTypesEvent = 1028;
    public const uint GetCollectibleMintingEnabledEvent = 2719;
    public const uint MintCollectibleEvent = 3957;
    public const uint GetNftClaimsEvent = 1352;
    public const uint ClaimNftRewardsEvent = 501;

    //Preferences
    public const uint SetChatStylePreferenceEvent = 3519;
    public const uint SetChatPreferenceEvent = 65476;

    // Catalog
    public const uint GetCatalogPageEvent = 1211;
    public const uint GetProductOfferEvent = 1020;
    public const uint GetKickbackInfoEvent = 83;
    public const uint GetClubGiftInfoEvent = 2451;
    public const uint PurchaseFromCatalogEvent = 1099;
    public const uint PurchaseFromCatalogAsGiftEvent = 3581;
    public const uint GetBundleDiscountRulesetEvent = 1103;

    // Polls
    public const uint PollStartEvent = 2864;
    public const uint PollRejectEvent = 1876;
    public const uint PollAnswerEvent = 3552;

    // Navigator

    // Messenger
    public const uint GetFriendRequestsEvent = 3234;

    // Quests
    public const uint GetQuestListEvent = 3813;
    public const uint StartQuestEvent = 570;
    public const uint GetCurrentQuestEvent = 927;
    public const uint CancelQuestEvent = 1600;

    // Room Avatar
    public const uint ActionEvent = 1697;
    public const uint ApplySignEvent = 1453;
    public const uint DanceEvent = 2785;
    public const uint SitEvent = 281;
    public const uint ChangeMottoEvent = 51;
    public const uint LookToEvent = 638;
    public const uint DropHandItemEvent = 1376;

    // Room Connection
    public const uint OpenFlatConnectionEvent = 3835;
    public const uint GoToFlatEvent = 65442;

    // Room Chat
    public const uint ChatEvent = 612;
    public const uint ShoutEvent = 2857;
    public const uint WhisperEvent = 631;

    // Room Engine

    // Room Settings

    // Room Action

    // Users
    public const uint GetIgnoredUsersEvent = 680;

    // Moderation
    public const uint OpenHelpToolEvent = 1023;
    public const uint CallForHelpPendingCallsDeletedEvent = 3355;
    public const uint ModeratorActionEvent = 984;
    public const uint ModerationMsgEvent = 2197;
    public const uint ModerationMuteEvent = 2460;
    public const uint ModerationTradeLockEvent = 1180;
    public const uint GetModeratorUserRoomVisitsEvent = 43;
    public const uint ModerationKickEvent = 302;
    public const uint GetModeratorRoomInfoEvent = 3998;
    public const uint GetModeratorUserInfoEvent = 3618;
    public const uint GetModeratorRoomChatlogEvent = 3499;
    public const uint ModerateRoomEvent = 2604;
    public const uint GetModeratorUserChatlogEvent = 1219;
    public const uint GetModeratorTicketChatlogsEvent = 711;
    public const uint ModerationCautionEvent = 3167;
    public const uint ModerationBanEvent = 1054;
    public const uint SubmitNewTicketEvent = 1409;
    public const uint CloseIssueDefaultActionEvent = 2730;

    // Inventory
    public const uint GetCreditsInfoEvent = 511;
    public const uint GetAchievementsEvent = 3338;
    public const uint GetBadgesEvent = 152;
    public const uint RequestFurniInventoryEvent = 3340;
    public const uint SetActivatedBadgesEvent = 2416;
    public const uint AvatarEffectActivatedEvent = 3529;
    public const uint AvatarEffectSelectedEvent = 2236;

    public const uint InitTradeEvent = 1228;
    public const uint TradingCancelConfirmEvent = 3765;
    public const uint TradingModifyEvent = 3905;
    public const uint TradingOfferItemEvent = 2511;
    public const uint TradingCancelEvent = 3991;
    public const uint TradingConfirmEvent = 1654;
    public const uint TradingOfferItemsEvent = 3111;
    public const uint TradingRemoveItemEvent = 3461;
    public const uint TradingAcceptEvent = 3053;

    // Register
    public const uint UpdateFigureDataEvent = 1587;

    // Groups
    public const uint GetBadgeEditorPartsEvent = 3413;
    public const uint GetGroupCreationWindowEvent = 3713;
    public const uint GetGroupFurniSettingsEvent = 3823;
    public const uint DeclineGroupMembershipEvent = 3930;
    public const uint JoinGroupEvent = 3178;
    public const uint UpdateGroupColoursEvent = 2478;
    public const uint SetGroupFavouriteEvent = 3323;
    public const uint GetGroupMembersEvent = 440;

    // Group Forums
    public const uint PostGroupContentEvent = 1958;
    public const uint GetForumStatsEvent = 1875;
    public const uint GetForumThreadEvent = 253;
    public const uint UpdateForumReadMarkersEvent = 1933;
    public const uint GetForumsUnreadCountEvent = 3094;

    // Sound


    // Ambassador

    public const uint AmbassadorAlertEvent = 336;

    public const uint RemoveMyRightsEvent = 2775;
    public const uint GiveHandItemEvent = 2657;
    public const uint GoToHotelViewEvent = 914;
    public const uint GetRoomFilterListEvent = 2749;
    public const uint GetPromoArticlesEvent = 1963;
    public const uint ModifyWhoCanRideHorseEvent = 2263;
    public const uint RemoveFriendEvent = 824;
    public const uint RefreshCampaignEvent = 3294;
    public const uint AcceptFriendEvent = 272;
    public const uint YouTubeVideoInformationEvent = 65401; // The client has no composer for this id; 2488 is TryPhoneNumber
    public const uint FollowFriendEvent = 1703;
    public const uint SaveBotActionEvent = 3463;
    public const uint LetUserInEvent = 3446;
    public const uint GetMarketplaceItemStatsEvent = 3359;
    public const uint GetSellablePetPalettesEvent = 3521;
    public const uint OpenCampaignCalendarDoorAsStaffEvent = 2602;
    public const uint SetUIFlagsEvent = 3774;
    public const uint DeleteRoomEvent = 1238;
    public const uint SetSoundSettingsEvent = 781;
    public const uint InitializeGameCenterEvent = 65468;
    public const uint RedeemOfferCreditsEvent = 2483;
    public const uint FriendListUpdateEvent = 2877;
    public const uint FriendFurniConfirmLockEvent = 1689;
    public const uint UseHabboWheelEvent = 861;
    public const uint SaveRoomSettingsEvent = 1423;
    public const uint ToggleMoodlightEvent = 3638;
    public const uint GetDailyQuestEvent = 1380;
    public const uint SetMannequinNameEvent = 153;
    public const uint OneWayGateEvent = 2577;
    public const uint EventTrackerEvent = 65429;
    public const uint GetOccupiedTilesEvent = 3345;
    public const uint PickUpPetEvent = 495;
    public const uint GetPetInventoryEvent = 1817;
    public const uint GetRoomEntryTileEvent = 2198;
    public const uint GetOwnOffersEvent = 1595;
    public const uint CheckPetNameEvent = 1393;
    public const uint SetUserFocusPreferenceEvent = 3931;
    public const uint SubmitBullyReportEvent = 3140;
    public const uint RemoveRightsEvent = 2259;
    public const uint MakeOfferEvent = 371;
    public const uint KickUserEvent = 391;
    public const uint GetRoomSettingsEvent = 2288;
    public const uint GetThreadsListDataEvent = 2942;
    public const uint GetForumUserProfileEvent = 2032;
    public const uint OpenWiredEvent = 182;
    public const uint WiredMonitorRequestEvent = 10021;
    public const uint WiredRoomLogsPageEvent = 2114;
    public const uint WiredRoomSettingsRequestEvent = 10022;
    public const uint WiredRoomSettingsSaveEvent = 10023;
    public const uint WiredGetRoomSettingsEvent = 3483;
    public const uint WiredSetRoomSettingsEvent = 452;
    public const uint WiredMenuPermissionsSaveEvent = 65465;
    public const uint WiredRoomUpdateEvent = 817;
    public const uint ClickFurniEvent = 2056;
    public const uint ClickUserEvent = 10020;
    public const uint WiredUserVariablesRequestEvent = 10024;
    public const uint WiredUserVariableUpdateEvent = 10025;
    public const uint WiredUserVariableManageEvent = 10026;
    public const uint WiredVariableHoldersPageEvent = 65307;
    public const uint WiredVariableHoldersRequestEvent = 65441;
    public const uint WiredVariableHashesEvent = 1084;
    public const uint WiredAllVariablesRequestEvent = 3110;
    public const uint SaveWiredEffectConfigEvent = 553;
    public const uint ApplySnapshotEvent = 31;
    public const uint SaveWiredSelectorConfigEvent = 1119;
    public const uint SaveWiredAddonConfigEvent = 3766;
    public const uint SaveWiredVariableConfigEvent = 3999;
    public const uint GetRoomEntryDataEvent = 65460;
    public const uint JoinQueueEvent = 65471;
    public const uint CanCreateRoomEvent = 3643;
    public const uint SetTonerEvent = 1197;
    public const uint RecolorFurnitureEvent = 672;
    public const uint SaveWiredTriggerConfigEvent = 606;
    public const uint PlaceBotEvent = 2203;
    public const uint MoveEntityInFlatEvent = 583;
    public const uint GetRelationshipsEvent = 2228;
    public const uint SetMessengerInviteStatusEvent = 2674;
    public const uint UseFurnitureEvent = 16;
    public const uint GetUserFlatCatsEvent = 1613;
    public const uint AssignRightsEvent = 1851;
    public const uint GetRoomBannedUsersEvent = 3423;
    public const uint ReleaseTicketEvent = 488;
    public const uint OpenPlayerProfileEvent = 3639;
    public const uint GetSanctionStatusEvent = 65447;
    public const uint CreditFurniRedeemEvent = 2866;
    public const uint DisconnectEvent = 1022;
    public const uint PickupObjectEvent = 2408;
    public const uint FindRandomFriendingRoomEvent = 1615;
    public const uint UseSellableClothingEvent = 1334;
    public const uint MoveObjectEvent = 2278;
    public const uint GetFurnitureAliasesEvent = 1814;
    public const uint TakeAdminRightsEvent = 1604;
    public const uint ModifyRoomFilterListEvent = 3928;
    public const uint MoodlightUpdateEvent = 3650;
    public const uint GetPetTrainingPanelEvent = 3493;
    public const uint GetSongInfoEvent = 1596;
    public const uint UseWallItemEvent = 3737;
    public const uint GetTalentTrackEvent = 258;
    public const uint GiveAdminRightsEvent = 550;
    public const uint GetCatalogIndexEvent = 3437;
    public const uint BuildersClubPlaceRoomItemEvent = 1235;
    public const uint BuildersClubPlaceWallItemEvent = 1721;
    public const uint GetGuideReportingStatusEvent = 2394;
    public const uint CancelOfferEvent = 3285;
    public const uint SaveWiredConditionConfigEvent = 246;
    public const uint RedeemVoucherEvent = 2004;
    public const uint ThrowDiceEvent = 3543;
    public const uint CraftSecretEvent = 3564;
    public const uint GetGameListEvent = 65391;
    public const uint SetRelationshipEvent = 1363;
    public const uint RequestFriendEvent = 1787;
    public const uint MemoryPerformanceEvent = 2269;
    public const uint ToggleYouTubeVideoEvent = 1584;
    public const uint SetMannequinFigureEvent = 188;
    public const uint SetClothingChangeDataEvent = 3398;
    public const uint GetEventCategoriesEvent = 1944;
    public const uint DeleteGroupThreadEvent = 876;
    public const uint PurchaseGroupEvent = 1291;
    public const uint MessengerInitEvent = 2750;
    public const uint CancelTypingEvent = 2063;
    public const uint GetMoodlightConfigEvent = 2971;
    public const uint GetGroupInfoEvent = 1075;
    public const uint CreateFlatEvent = 3623;
    public const uint LatencyPingRequestEvent = 961;
    public const uint GetSelectedBadgesEvent = 3674;
    public const uint AddStickyNoteEvent = 75;
    public const uint RideHorseEvent = 3541;
    public const uint InitializeNewNavigatorEvent = 1490;
    public const uint GetForumsListDataEvent = 2066;
    public const uint ToggleMuteToolEvent = 2074;
    public const uint UpdateGroupIdentityEvent = 1155;
    public const uint UpdateStickyNoteEvent = 1131;
    public const uint UnbanUserFromRoomEvent = 1871;
    public const uint UnignoreUserEvent = 850;
    public const uint OpenGiftEvent = 1922;
    public const uint ApplyDecorationEvent = 2527;
    public const uint ScrGetUserInfoEvent = 2396;
    public const uint RemoveGroupMemberEvent = 3839;
    public const uint ConfirmRemoveGroupMemberEvent = 820;
    public const uint DiceOffEvent = 849;
    public const uint YouTubeGetNextVideo = 3898;
    public const uint RemoveFavouriteRoomEvent = 3590;
    public const uint RespectUserEvent = 2614;
    public const uint AddFavouriteRoomEvent = 1198;
    public const uint DeclineFriendEvent = 1531;
    public const uint StartTypingEvent = 22;
    public const uint GetGroupFurniConfigEvent = 2947;
    public const uint SendRoomInviteEvent = 2093;
    public const uint RemoveAllRightsEvent = 1384;
    public const uint GetYouTubeTelevisionEvent = 2562;
    public const uint FindNewFriendsEvent = 3014;
    public const uint GetPromotableRoomsEvent = 2546;
    public const uint GetBotInventoryEvent = 1106;
    public const uint GetRentableSpaceEvent = 1013;
    public const uint OpenBotActionEvent = 3332;
    public const uint OpenCampaignCalendarDoorEvent = 3310;
    public const uint DeleteGroupPostEvent = 900;
    public const uint UpdateGroupBadgeEvent = 403;
    public const uint PlaceObjectEvent = 2080;
    public const uint RemoveGroupFavouriteEvent = 308;
    public const uint UpdateNavigatorSettingsEvent = 2744;
    public const uint CheckGnomeNameEvent = 2329;
    public const uint NavigatorSearchEvent = 394;
    public const uint GetPetInformationEvent = 1132;
    public const uint GetGuestRoomEvent = 549;
    public const uint UpdateThreadEvent = 2382;
    public const uint AcceptGroupMembershipEvent = 1372;
    public const uint GetMarketplaceConfigurationEvent = 2786;
    public const uint Game2GetWeeklyLeaderboardEvent = 3095;
    public const uint BuyOfferEvent = 2392;
    public const uint RemoveSaddleFromHorseEvent = 3210;
    public const uint GiveRoomScoreEvent = 127;
    public const uint GetHabboClubWindowEvent = 1284;
    public const uint DeleteStickyNoteEvent = 2430;
    public const uint MuteUserEvent = 1501;
    public const uint ApplyHorseEffectEvent = 2229;
    public const uint ClientHelloEvent = 4000; //4000
    public const uint OnBullyClickEvent = 65455;
    public const uint HabboSearchEvent = 1707;
    public const uint PickTicketEvent = 474;
    public const uint GetGiftWrappingConfigurationEvent = 1740;
    public const uint GetCraftingRecipesAvailableEvent = 1379;
    public const uint GetThreadDataEvent = 3683;
    public const uint ManageGroupEvent = 3341;
    public const uint PlacePetEvent = 3230;
    public const uint MovePetEvent = 3443;
    public const uint EditRoomPromotionEvent = 959;
    public const uint UpdateFloorPropertiesEvent = 1104;
    public const uint MoveWallItemEvent = 7;
    public const uint VersionCheckEvent = 1326;
    public const uint PongEvent = 2903;
    public const uint DeleteGroupEvent = 378;
    public const uint UpdateGroupSettingsEvent = 2404;
    public const uint GetRecyclerPrizesEvent = 2353;
    public const uint PurchaseRoomAdEvent = 106;
    public const uint PickUpBotEvent = 1477;
    public const uint GetOffersEvent = 1039;
    public const uint GetHabboGroupBadgesEvent = 3762;
    public const uint GetUserTagsEvent = 65382;
    public const uint GetGameAchievementsEvent = 65475;
    public const uint GetCatalogRoomPromotionEvent = 624;
    public const uint MoveAvatarEvent = 2916;
    public const uint SaveBrandingItemEvent = 1416;
    public const uint SetAreaHideEvent = 575;
    public const uint SaveEnforcedCategorySettingsEvent = 1481;
    public const uint RespectPetEvent = 2475;
    public const uint GetMarketplaceCanMakeOfferEvent = 3972;
    public const uint UpdateMagicTileAdjacentEvent = 65449;
    public const uint UpdateMagicTileEvent = 3812;
    public const uint GetStickyNoteEvent = 2258;
    public const uint IgnoreUserEvent = 80;
    public const uint BanUserEvent = 1258;
    public const uint UpdateForumSettingsEvent = 1375;
    public const uint GetRoomRightsEvent = 162;
    public const uint SendMsgEvent = 3425;
    public const uint CloseTicketEvent = 564;

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
    public const uint GetHabboClubExtendOfferEvent = 2788;
    public const uint PurchaseBasicMembershipExtensionEvent = 2572;
    public const uint PurchaseVipMembershipExtensionEvent = 3678;
    public const uint SelectClubGiftEvent = 1222;


    //recycler
    public const uint GetRecyclerStatusEvent = 3901;
    public const uint RecyclerRecycleEvent = 3050;


    //FriendList
    //public const uint FriendFurniConfirmLockEvent =;
    //public const uint VisitUserEvent =;

    //crafting
    public const uint CraftEvent = 1919;
    //public const uint CraftSecretEvent =;
    public const uint GetCraftableProductsEvent = 2100;
    public const uint GetCraftingRecipeEvent = 144;
    //public const uint GetCraftingRecipesAvailableEvent =;

    // SnowStorm (AIR Game2).
    public const uint Game2CheckGameDirectoryStatusEvent = 2501;
    public const uint Game2GetAccountGameStatusEvent = 2405;
    public const uint Game2QuickJoinEvent = 3518;
    public const uint Game2LeaveLobbyEvent = 877;
    public const uint Game2LoadStageReadyEvent = 3023;
    public const uint Game2ExitGameEvent = 1952;
    public const uint Game2GameChatEvent = 3009;
    public const uint Game2PlayAgainEvent = 2141;
    public const uint Game2SetUserMoveTargetEvent = 1657;
    public const uint Game2ThrowSnowballAtPositionEvent = 1805;
    public const uint Game2ThrowSnowballAtHumanEvent = 2787;
    public const uint Game2MakeSnowballEvent = 1520;
    public const uint Game2RequestFullStatusUpdateEvent = 1837;
    public const uint Game2GetTotalLeaderboardEvent = 1955;
    public const uint Game2GetFriendsLeaderboardEvent = 1424;
    public const uint Game2GetWeeklyFriendsLeaderboardEvent = 1515;
    public const uint Game2GetTotalGroupLeaderboardEvent = 3792;
    public const uint Game2GetWeeklyGroupLeaderboardEvent = 1583;
    public const uint GetSnowWarGameTokensOfferEvent = 445;
    public const uint PurchaseSnowWarGameTokensOfferEvent = 3308;




    public const uint TriggerHabbiconEvent = 2646;
    public const uint GetHabbiconShopDataEvent = 3579;
    public const uint GetHabbiconInfoEvent = 2196;
    public const uint BuyHabbiconEvent = 1473;
    public const uint BuyHabbiconCollectionEvent = 3126;
    public const uint ClaimHabbiconEvent = 1457;
    public const uint FavoriteHabbiconEvent = 512;
    public const uint UnfavoriteHabbiconEvent = 3016;
    public const uint UnseenResetCategoryEvent = 65428;
    public const uint UnseenResetItemsEvent = 65459;
    public const uint SendMessengerMessageEvent = 4902;
    // Camera internal IDs; wire IDs are revision-specific.
    public const uint InitCameraEvent = 1754;
    public const uint PhotoCompetitionEvent = 2831;
    public const uint PublishPhotoEvent = 2694;
    public const uint PurchasePhotoEvent = 1492;
    public const uint RenderRoomEvent = 3217;
    public const uint RenderRoomThumbnailEvent = 1819;
    public const uint GetRewardTracksEvent = 9450;
    public const uint ClaimRewardTrackPrizeEvent = 1979;
    public const uint PurchaseRewardTrackPremiumEvent = 3827;
    // Daily tasks.
    public const uint GetDailyTasksEvent = 4100;
    public const uint ClaimDailyTaskEvent = 4101;
    public const uint GetQuizQuestionsEvent = 1213;
    public const uint PostQuizAnswersEvent = 704;
    // Room music
    public const uint AddJukeboxDiskEvent = 1133;
    public const uint RemoveJukeboxDiskEvent = 1662;
    public const uint GetJukeboxPlaylistEvent = 2543;
    public const uint GetSoundMachinePlaylistEvent = 1777;
    public const uint GetUserSongDisksEvent = 3220;
    public const uint GetNowPlayingEvent = 3524;
    public const uint GetOfficialSongIdEvent = 323;
    public const uint WiredVariableInspectionRequestEvent = 10112;
    public const uint WiredUserVariableUpdate64Event = 10110;
    public const uint WiredUserVariableManage64Event = 10111;
    public const uint WiredUserVariablesRequest64Event = 10113;
    public const uint WiredVariableHoldersRequest64Event = 10114;
    public const uint WiredVariableHoldersPage64Event = 10115;
    // Wired chests and wired trades; see reviews/chest-protocol-map.md.
    public const uint ChestOpenEvent = 2038;
    public const uint ChestCloseEvent = 2599;
    public const uint ChestStartDepositEvent = 2299;
    public const uint ChestWithdrawAllEvent = 3567;
    public const uint ChestWithdrawFurniEvent = 1589;
    public const uint ChestWithdrawCoinsEvent = 2073;
    public const uint ChestSaveOptionsEvent = 785;
    public const uint ChestSavePreferencesEvent = 2976;
    public const uint ChestSaveNotificationsEvent = 1274;
    public const uint WiredChestOfferItemsEvent = 1882;
    public const uint WiredChestAcceptEvent = 3369;
    public const uint WiredChestCancelEvent = 3390;
    public const uint ChestUpgradeEvent = 361;
    public const uint WiredChestLockEvent = 3787;

    // October wired boxes.
    public const uint WiredWebApiKeyRequestEvent = 971;
    public const uint WiredVariableShareSynchronizeEvent = 2050;
    public const uint WiredVariableLeaderboardSubscribeEvent = 3982;
    public const uint SaveWiredLabelTextEvent = 3113;
    public const uint SaveWiredLabelSettingsEvent = 2779;
    public const uint WiredLinkRequestConfigEvent = 3358;
    public const uint WiredLinkSaveSelectionEvent = 2088;
    public const uint WiredLinkCreateNetworkEvent = 3953;
    public const uint WiredLinkRenameNetworkEvent = 3773;
    public const uint WiredLinkDeleteNetworkEvent = 2026;
    public const uint WiredLinkGetConnectionsEvent = 3641;
    public const uint GetWiredContractContentsEvent = 343;
    public const uint UpdateWiredContractEvent = 1732;
    // Wired monitor. The monitor tab sends both requests together.
    public const uint GetWiredRoomStatsEvent = 2907;
    public const uint GetWiredErrorLogsEvent = 441;
    public const uint ClearWiredErrorLogsEvent = 1714;
    // The inspection tab's value edit, create and delete.
    public const uint WiredSetObjectVariableValueEvent = 3831;
    // The inspection tab's read of one object's variables.
    public const uint WiredGetVariablesForObjectEvent = 2768;

    // Free product claims (cursed dragon adoption) and the new-feature promo expiry.
    public const uint HasClaimedProductEvent = 2640;
    public const uint ClaimProductEvent = 3651;
    public const uint GetSecondsUntilEvent = 2389;
}
