namespace Plus.Communication.Packets.Outgoing;

public static class ServerPacketHeader
{
    public const uint UserPurchasableChatStylesComposer = 470;
    public const uint UserPurchasableChatStyleChangedComposer = 916;
    // Handshake
    public const uint SimplePollStartComposer = 688;
    public const uint SimplePollAnswerComposer = 3576;
    public const uint SimplePollAnswersComposer = 3645;
    public const uint InitDiffieHandshakeComposer = 1823;
    public const uint SecretKeyComposer = 3796;
    public const uint AuthenticationOkComposer = 599;
    public const uint UserObjectComposer = 1168;
    public const uint UserPerksComposer = 420;
    public const uint KickbackInfoComposer = 3923;
    public const uint UserRightsComposer = 2446;
    public const uint GenericErrorComposer = 2097;
    public const uint DisconnectReasonComposer = 4000;
    public const uint SetUniqueIdComposer = 65511;
    public const uint AvailabilityStatusComposer = 515;

    // Avatar
    public const uint WardrobeComposer = 1192;
    public const uint UserNftWardrobeComposer = 1458;
    public const uint SelectedNftWardrobeOutfitComposer = 2547;

    // Collectibles hub
    public const uint CollectibleWalletAddressesComposer = 1645;
    public const uint CollectorScoreComposer = 2899;
    public const uint NftCollectionsComposer = 70;
    public const uint NftCollectionBonusClaimResultComposer = 1901;
    public const uint NftCollectionRewardClaimResultComposer = 2380;
    public const uint NftStoreOffersComposer = 177;
    public const uint NftStorePurchaseResultComposer = 3694;
    public const uint CollectibleMintableItemTypesComposer = 2640;
    public const uint CollectibleMintingEnabledComposer = 57;
    public const uint MintCollectibleResultComposer = 2964;
    public const uint NftClaimsComposer = 2802;
    public const uint NftClaimResultComposer = 2361;
    public const uint EmeraldBalanceComposer = 318;

    // Catalog
    public const uint CatalogIndexComposer = 2830;
    public const uint CatalogItemDiscountComposer = 1420;
    public const uint BuildersClubSubscriptionStatusComposer = 565;
    public const uint BuildersClubFurniCountComposer = 3981;
    public const uint BuildersClubPlacementWarningComposer = 3510;
    public const uint PurchaseOKComposer = 3898;
    public const uint CatalogOfferComposer = 2626;
    public const uint CatalogPageComposer = 1607;
    public const uint CatalogUpdatedComposer = 1932;
    public const uint HabboClubOffersComposer = 3053;
    public const uint SellablePetBreedsComposer = 1663;
    public const uint GroupFurniConfigComposer = 3813;
    public const uint PresentDeliverErrorComposer = 3609;

    // Quests
    public const uint QuestListComposer = 2838;
    public const uint QuestCompletedComposer = 3913;
    public const uint QuestAbortedComposer = 1200;
    public const uint QuestStartedComposer = 2364;

    // Room Avatar
    public const uint ActionComposer = 447;
    public const uint SleepComposer = 1788;
    public const uint DanceComposer = 1248;
    public const uint CarryObjectComposer = 1548;
    public const uint AvatarEffectComposer = 1839;

    // Room Chat
    public const uint ChatComposer = 3477;
    public const uint ShoutComposer = 1206;
    public const uint WhisperComposer = 2229;
    public const uint FloodControlComposer = 1155;
    public const uint UserTypingComposer = 3738;

    // Room Engine
    public const uint UsersComposer = 3357;
    public const uint FurnitureAliasesComposer = 3584;
    public const uint ObjectAddComposer = 824;
    public const uint ObjectsComposer = 546;
    public const uint ObjectUpdateComposer = 1124;
    public const uint ObjectDataUpdateComposer = 1172;
    public const uint ObjectsDataUpdateComposer = 1122;
    public const uint FurniRecolorUpdateComposer = 2810;
    public const uint ObjectRemoveComposer = 1425;
    public const uint ObjectRemoveMultipleComposer = 1782;
    public const uint SlideObjectBundleComposer = 1983;
    public const uint ItemsComposer = 3060;
    public const uint ItemAddComposer = 2177;
    public const uint ItemUpdateComposer = 3398;
    // Plus-only id, not an official one: furni metadata for the entries above. 5120 is free in the client's and Volt-Renderer's
    // incoming tables.
    public const uint FurnitureMetadataComposer = 5120;
    public const uint ItemRemoveComposer = 3232;

    // Room Session
    public const uint RoomForwardComposer = 3757;
    public const uint RoomReadyComposer = 1256;
    public const uint OpenConnectionComposer = 2833;
    public const uint CloseConnectionComposer = 987;
    public const uint FlatAccessibleComposer = 2497;
    public const uint CantConnectComposer = 3889;

    // Room Permissions
    public const uint YouAreControllerComposer = 3452;
    public const uint YouAreNotControllerComposer = 2077;
    public const uint YouAreOwnerComposer = 2164;

    // Room Settings
    public const uint RoomSettingsDataComposer = 933;
    public const uint RoomSettingsSavedComposer = 1000;
    public const uint FlatControllerRemovedComposer = 3184;
    public const uint FlatControllerAddedComposer = 1907;
    public const uint RoomRightsListComposer = 3347;

    // Room Furniture
    public const uint WiredValidationErrorComposer = 2646;
    public const uint WiredMonitorDataComposer = 5101;
    public const uint WiredRoomLogPageComposer = 1508;
    public const uint WiredRoomSettingsDataComposer = 5102;
    public const uint WiredRoomSettingsComposer = 348;
    public const uint WiredRewardResultComposer = 529;
    public const uint WiredMovementsComposer = 67;
    public const uint WiredClickSettingsComposer = 2251;
    public const uint WiredClickUserResponseComposer = 2855;
    public const uint InClientLinkComposer = 1522;
    public const uint WiredVariableFxConfigsComposer = 1128;
    public const uint WiredVariableFxConfigsRemovedComposer = 2302;
    public const uint WiredVariableFxStatusComposer = 1427;
    public const uint WiredVariableFxStatusRemovedComposer = 760;
    public const uint WiredUserVariablesDataComposer = 5103;
    public const uint WiredAllVariablesHashComposer = 2007;
    public const uint WiredAllVariablesDiffComposer = 517;
    public const uint WiredVariableHoldersPageComposer = 9461;
    public const uint WiredVariableHoldersComposer = 9462;
    public const uint HideWiredConfigComposer = 3747;
    public const uint WiredEffectConfigComposer = 886;
    public const uint WiredSelectorConfigComposer = 2637;
    public const uint WiredAddonConfigComposer = 77;
    public const uint WiredVariableConfigComposer = 898;
    public const uint WiredConditionConfigComposer = 922;
    public const uint WiredTriggeRconfigComposer = 3252;
    public const uint MoodlightConfigComposer = 2511;
    public const uint GroupFurniSettingsComposer = 73;
    public const uint OpenGiftComposer = 1024;

    // Navigator
    public const uint UpdateFavouriteRoomComposer = 1965;
    public const uint NavigatorLiftedRoomsComposer = 818;
    public const uint NavigatorPreferencesComposer = 2191;
    public const uint NavigatorFlatCatsComposer = 1185;
    public const uint NavigatorMetaDataParserComposer = 123;
    public const uint NavigatorCollapsedCategoriesComposer = 414;

    // Messenger
    public const uint BuddyListComposer = 861;
    public const uint FriendRequestsComposer = 3700;
    public const uint NewBuddyRequestComposer = 3055;

    // Moderation
    public const uint ModeratorInitComposer = 2067;
    public const uint ModeratorUserRoomVisitsComposer = 3243;
    public const uint ModeratorRoomChatlogComposer = 893;
    public const uint ModeratorUserInfoComposer = 3498;
    public const uint ModeratorSupportTicketResponseComposer = 3322;
    public const uint ModeratorUserChatlogComposer = 528;
    public const uint ModeratorRoomInfoComposer = 1406;
    public const uint ModeratorSupportTicketComposer = 2204;
    public const uint ModeratorTicketChatlogComposer = 3059;
    public const uint CallForHelpPendingCallsComposer = 2944;
    public const uint CfhTopicsInitComposer = 2328;

    // Inventory
    public const uint CreditBalanceComposer = 231;
    public const uint BadgesComposer = 2887;
    public const uint FurniListAddComposer = 2857;
    public const uint FurniListNotificationComposer = 178;
    public const uint FurniListRemoveComposer = 883;
    public const uint FurniListComposer = 2911;
    public const uint FurniListUpdateComposer = 1426;
    public const uint AvatarEffectsComposer = 1353;
    public const uint AvatarEffectActivatedComposer = 514;
    public const uint AvatarEffectExpiredComposer = 2133;
    public const uint AvatarEffectAddedComposer = 261;
    public const uint TradingErrorComposer = 3147;
    public const uint TradingAcceptComposer = 1290;
    public const uint TradingStartComposer = 2861;
    public const uint TradingUpdateComposer = 2934;
    public const uint TradingClosedComposer = 1195;
    public const uint TradingCompleteComposer = 785;
    public const uint TradingFinishComposer = 336;

    // Inventory Achievements
    public const uint AchievementsComposer = 3693;
    public const uint AchievementScoreComposer = 273;
    public const uint AchievementUnlockedComposer = 1882;
    public const uint AchievementProgressedComposer = 3556;

    // Notifications
    public const uint ActivityPointsComposer = 931;
    public const uint HabboActivityPointNotificationComposer = 3154;

    // Users
    public const uint ScrSendUserInfoComposer = 2958;
    public const uint IgnoredUsersComposer = 338;

    // Groups
    public const uint UnknownGroupComposer = 2990;
    public const uint GroupMembershipRequestedComposer = 2216;
    public const uint ManageGroupComposer = 3051;
    public const uint HabboGroupBadgesComposer = 1750;
    public const uint NewGroupInfoComposer = 1201;
    public const uint GroupInfoComposer = 3702;
    public const uint GroupCreationWindowComposer = 981;
    public const uint SetGroupIdComposer = 1781;
    public const uint GroupMembersComposer = 887;
    public const uint UpdateFavouriteGroupComposer = 135;
    public const uint GroupMemberUpdatedComposer = 1315;
    public const uint GroupConfirmRemoveMemberComposer = 65505;
    public const uint GroupDeactivatedComposer = 75;
    public const uint RefreshFavouriteGroupComposer = 201;

    // Group Forums
    public const uint ForumsListDataComposer = 2577;
    public const uint ForumDataComposer = 1709;
    public const uint ThreadCreatedComposer = 246;
    public const uint ThreadDataComposer = 3512;
    public const uint ThreadsListDataComposer = 488;
    public const uint ThreadUpdatedComposer = 3554;
    public const uint ThreadReplyComposer = 1786;
    public const uint ForumsUnreadCountComposer = 1160;

    // Sound
    public const uint SoundSettingsComposer = 2180;

    public const uint AvatarAspectUpdateComposer = 287;
    public const uint HelperToolComposer = 97;
    public const uint RoomErrorNotifComposer = 1035;
    public const uint FollowFriendFailedComposer = 130;

    public const uint FindFriendsProcessResultComposer = 1459;
    public const uint UserChangeComposer = 3470;
    public const uint FloorHeightMapComposer = 1530;
    public const uint RoomInfoUpdatedComposer = 2313;
    public const uint MessengerErrorComposer = 3105;
    public const uint MarketplaceCanMakeOfferResultComposer = 2929;
    public const uint GameAccountStatusComposer = 3367;
    public const uint GuestRoomSearchResultComposer = 2215;
    public const uint NewUserExperienceGiftOfferComposer = 1142;
    public const uint UpdateUsernameComposer = 2644;
    public const uint VoucherRedeemOkComposer = 511;
    public const uint FigureSetIdsComposer = 242;
    public const uint StickyNoteComposer = 1282;
    public const uint UserRemoveComposer = 2675;
    public const uint GetGuestRoomResultComposer = 892;
    public const uint DoorbellComposer = 3520;

    public const uint GiftWrappingConfigurationComposer = 1331;
    public const uint GetRelationshipsComposer = 3056;
    public const uint FriendNotificationComposer = 2928;
    public const uint BadgeEditorPartsComposer = 845;
    public const uint TraxSongInfoComposer = 701;
    public const uint PostUpdatedComposer = 2266;
    public const uint UserUpdateComposer = 996;
    public const uint MutedComposer = 924;
    public const uint MarketplaceConfigurationComposer = 2669;
    public const uint CheckGnomeNameComposer = 2634;
    public const uint OpenBotActionComposer = 1584;
    public const uint FavouritesComposer = 1914;
    public const uint TalentLevelUpComposer = 3305;

    public const uint UserTagsComposer = 65514;
    public const uint CampaignComposer = 653;
    public const uint RoomEventComposer = 3909;
    public const uint MarketplaceItemStatsComposer = 1275;
    public const uint HabboSearchResultComposer = 3423;
    public const uint PetHorseFigureInformationComposer = 1754;
    public const uint PetInventoryComposer = 2701;
    public const uint PingComposer = 2935;
    public const uint RentableSpaceComposer = 2129;
    public const uint GetYouTubePlaylistComposer = 606;
    public const uint RespectNotificationComposer = 1414;
    public const uint RecyclerPrizesComposer = 1129;
    public const uint RecyclerStatusComposer = 1312;
    public const uint RecyclerFinishedComposer = 2056;
    public const uint GetRoomBannedUsersComposer = 2047;
    public const uint RoomRatingComposer = 404;
    public const uint PlayableGamesComposer = 65482;
    public const uint TalentTrackLevelComposer = 3598;
    public const uint JoinQueueComposer = 65499;
    public const uint MarketPlaceOwnOffersComposer = 374;
    public const uint PetBreedingComposer = 2572;
    public const uint SubmitBullyReportComposer = 49;
    public const uint UserNameChangeComposer = 1162;
    public const uint FriendFurniStartConfirmationComposer = 2425;
    public const uint GuideReportingStatusComposer = 3581;
    public const uint VoucherRedeemErrorComposer = 47;
    public const uint PurchaseErrorComposer = 2510;
    public const uint CampaignCalendarDoorOpenedComposer = 989;
    public const uint FriendListUpdateComposer = 1054;

    public const uint UserFlatCatsComposer = 2513;
    public const uint UpdateFreezeLivesComposer = 1929;
    public const uint UnbanUserFromRoomComposer = 2136;
    public const uint PetTrainingPanelComposer = 2476;
    public const uint FlatAccessDeniedComposer = 1964;
    public const uint LatencyPingResponseComposer = 3231;
    public const uint HabboUserBadgesComposer = 3377;
    public const uint HeightMapUpdateComposer = 805;
    public const uint HeightMapComposer = 3358;

    public const uint CanCreateRoomComposer = 2923;
    public const uint InstantMessageErrorComposer = 2044;
    public const uint GnomeBoxComposer = 3291;
    public const uint IgnoreStatusComposer = 3110;
    public const uint PetInformationComposer = 1257;
    public const uint NavigatorSearchResultSetComposer = 896;
    public const uint ConcurrentUsersGoalProgressComposer = 2988;
    public const uint VideoOffersRewardsComposer = 2965;
    public const uint SanctionStatusComposer = 1763;
    public const uint GetYouTubeVideoComposer = 3960;
    public const uint CheckPetNameComposer = 2901;
    public const uint RespectPetNotificationComposer = 2075;
    public const uint EnforceCategoryUpdateComposer = 1304;
    public const uint CommunityGoalHallOfFameComposer = 638;
    public const uint RoomOccupiedTilesComposer = 2604;
    public const uint SendGameInvitationComposer = 65415;
    public const uint GiftWrappingErrorComposer = 3374;
    public const uint PromoArticlesComposer = 1588;
    public const uint Game1WeeklyLeaderboardComposer = 65423;
    public const uint RentableSpacesErrorComposer = 1153;
    public const uint AddExperiencePointsComposer = 2214;
    public const uint GetRoomFilterListComposer = 91;
    public const uint GameAchievementListComposer = 537;
    public const uint PromotableRoomsComposer = 3490;
    public const uint RoomEntryTileComposer = 570;
    public const uint RoomEntryInfoComposer = 2460;
    public const uint RoomNotificationComposer = 2760;
    public const uint ClubGiftsComposer = 2694;
    public const uint MOTDNotificationComposer = 1997;
    public const uint PopularRoomTagsResultComposer = 3201;
    public const uint NewConsoleMessageComposer = 1300;
    public const uint RoomPropertyComposer = 2880;
    public const uint MarketPlaceOffersComposer = 583;
    public const uint TalentTrackComposer = 716;
    public const uint ProfileInformationComposer = 1976;
    public const uint BadgeDefinitionsComposer = 1179;
    public const uint Game2WeeklyLeaderboardComposer = 1981;
    public const uint NameChangeUpdateComposer = 1894;
    public const uint RoomVisualizationSettingsComposer = 622;
    public const uint MarketplaceMakeOfferResultComposer = 2195;
    public const uint FlatCreatedComposer = 2543;
    public const uint BotInventoryComposer = 1784;
    public const uint LoadGameComposer = 65477;
    public const uint UpdateMagicTileComposer = 65486;
    public const uint MaintenanceStatusComposer = 1971;
    public const uint Game3WeeklyLeaderboardComposer = 3091;
    public const uint GameListComposer = 65500;
    public const uint RoomMuteSettingsComposer = 68;
    public const uint RoomInviteComposer = 2224;
    public const uint FriendFurniOtherLockConfirmedComposer = 2303;
    public const uint FriendFurniCancelLockComposer = 65420;
    public const uint BroadcastMessageAlertComposer = 367;
    public const uint ModeratorMessageComposer = 3409;
    public const uint MarketplaceCancelOfferResultComposer = 710;
    public const uint MarketplaceBuyOfferResultComposer = 506;
    public const uint NavigatorSettingsComposer = 2307;

    public const uint MessengerInitComposer = 3553;
    public const uint PollContentsComposer = 3651;
    public const uint PollOfferComposer = 3349;
    public const uint RoomUseHabbiconComposer = 2012;
    public const uint AllowedChatStylesComposer = 9340;
    public const uint CreatableRoomModelsComposer = 9341;
    public const uint UserHabbiconsComposer = 2618;
    public const uint UserHabbiconStatusChangedComposer = 3197;
    public const uint HabbiconShopDataComposer = 2458;
    public const uint HabbiconInfoComposer = 2502;
    public const uint MessengerMessageAckComposer = 4902;
    public const uint MessengerMessageFailedComposer = 4903;
    public const uint MessengerMessageComposer = 4904;
    // Camera internal IDs; wire IDs are revision-specific.
    public const uint InitCameraComposer = 1375;
    public const uint CameraStorageUrlComposer = 3927;
    public const uint CameraPurchaseOKComposer = 2272;
    public const uint CameraPublishStatusComposer = 3420;
    public const uint CompetitionStatusComposer = 495;
    public const uint ThumbnailStatusComposer = 997;
    public const uint RewardTracksComposer = 1236;
    public const uint RewardTrackClaimResultComposer = 1526;
    public const uint RewardTrackProgressComposer = 564;
    public const uint RewardTrackPremiumPurchaseResultComposer = 2072;
    // Daily tasks.
    public const uint ActiveDailyTasksComposer = 2636;
    public const uint DailyTasksAddedComposer = 717;
    public const uint DailyTaskUpdatedComposer = 3173;
    public const uint HabboClubExtendOfferComposer = 2582;
    public const uint ClubGiftReceivedComposer = 2367;
    public const uint PickMonthlyClubGiftComposer = 1261;
    // SnowStorm (AIR Game2).
    public const uint Game2GameDirectoryStatusComposer = 2791;
    public const uint Game2GameCreatedComposer = 3870;
    public const uint Game2InArenaQueueComposer = 2327;
    public const uint Game2GameLongDataComposer = 2278;
    public const uint Game2StartCounterComposer = 2024;
    public const uint Game2UserJoinedGameComposer = 767;
    public const uint Game2UserLeftGameComposer = 2006;
    public const uint Game2StopCounterComposer = 1821;
    public const uint Game2GameStartedComposer = 314;
    public const uint Game2EnterArenaComposer = 436;
    public const uint Game2ArenaEnteredComposer = 2089;
    public const uint Game2EnterArenaFailedComposer = 478;
    public const uint Game2GameStatusComposer = 3182;
    public const uint Game2FullGameStatusComposer = 963;
    public const uint Game2StageStartingComposer = 2706;
    public const uint Game2StageLoadComposer = 3065;
    public const uint Game2RejoinPreviousRoomComposer = 1416;
    public const uint Game2StageStillLoadingComposer = 2989;
    public const uint Game2GameEndingComposer = 1348;
    public const uint Game2GameChatComposer = 1653;
    public const uint Game2StageRunningComposer = 1031;
    public const uint Game2StageEndingComposer = 2850;
    public const uint Game2PlayerExitedGameArenaComposer = 1710;
    public const uint Game2PlayerRematchesComposer = 1831;
    public const uint Game2JoiningGameFailedComposer = 2238;
    public const uint Game2StartingGameFailedComposer = 2727;
    public const uint Game2GameCancelledComposer = 2332;
    public const uint Game2GameNotFoundComposer = 2423;
    public const uint Game2UserBlockedComposer = 88;
    public const uint Game2TotalLeaderboardComposer = 903;
    public const uint Game2FriendsLeaderboardComposer = 1334;
    public const uint Game2WeeklyFriendsLeaderboardComposer = 1817;
    public const uint Game2TotalGroupLeaderboardComposer = 1975;
    public const uint Game2WeeklyGroupLeaderboardComposer = 1130;
    public const uint SnowWarGameTokensComposer = 1050;
    public const uint CraftableProductsComposer = 487;
    public const uint CraftingRecipeComposer = 16;
    public const uint CraftingResultComposer = 180;
    public const uint CraftingRecipesAvailableComposer = 3449;
    // Room music
    public const uint JukeboxPlaylistComposer = 2202;
    public const uint JukeboxPlaylistFullComposer = 2399;
    public const uint NowPlayingComposer = 1568;
    public const uint OfficialSongIdComposer = 1156;
    public const uint SoundMachinePlaylistComposer = 747;
    public const uint SongDisksInventoryComposer = 948;
    public const uint QuizDataComposer = 3235;
    public const uint QuizResultsComposer = 1126;
    public const uint CampaignCalendarDataComposer = 846;
    public const uint WiredVariableInspectionDataComposer = 9483;
    public const uint WiredEnvironmentComposer = 2456;
    public const uint AreaHideComposer = 3726;
    public const uint ConfigurationItemStatesComposer = 248;
    public const uint UserNftChatStylesComposer = 3346;
    public const uint RaidProtectionCapabilityComposer = 1776;
    public const uint WiredUserVariablesData64Composer = 9480;
    public const uint WiredVariableHolders64Composer = 9481;
    public const uint WiredVariableHoldersPage64Composer = 9482;
    // Wired chests and wired transactions; see reviews/chest-protocol-map.md.
    public const uint WiredChestOpenComposer = 880;
    public const uint WiredChestSettingsUpdatedComposer = 1616;
    public const uint WiredChestUpgradeComposer = 2;
    public const uint WiredChestCoinsComposer = 1864;
    public const uint WiredChestFurniChunkComposer = 1094;
    public const uint WiredChestFurniUpdateComposer = 2619;
    public const uint WiredTransactionFailComposer = 2906;
    public const uint WiredTransactionSuccessComposer = 3682;
    public const uint WiredTradeErrorComposer = 563;
    public const uint WiredChestTradeOpenComposer = 512;
    public const uint WiredChestTradeItemsComposer = 1713;
    public const uint WiredChestTradeCancelledComposer = 3561;
    public const uint WiredChestTradeCompletedComposer = 1412;
    public const uint WiredTransactionLogListComposer = 3464;

    // October wired boxes.
    public const uint WiredFurniTransparencyComposer = 2702;
    public const uint WiredUserTransparencyComposer = 451;
    public const uint WiredFurniHighlightComposer = 2773;
    public const uint WiredWebApiKeyComposer = 2573;
    public const uint WiredVariableLeaderboardSnapshotComposer = 2087;
    public const uint WiredVariableLeaderboardChunkComposer = 2103;
    public const uint WiredLabelsComposer = 2491;
    public const uint WiredLabelsUpdateComposer = 1596;
    public const uint WiredLinkOpenComposer = 1803;
    public const uint WiredLinkConfigurationComposer = 3537;
    public const uint WiredLinkOperationResultComposer = 2463;
    public const uint WiredLinkConnectionsComposer = 2139;
    public const uint WiredLinkNetworkInvalidatedComposer = 3241;
    public const uint WiredUserHighlightComposer = 109;
    public const uint WiredFurniVisualizationStateComposer = 1176;
    public const uint WiredUserVisualizationStateComposer = 2282;
    public const uint ObjectRemoveConfirmComposer = 3555;
    public const uint YouArePlayingGameComposer = 2868;
    public const uint WiredContractOpenComposer = 1273;
    public const uint WiredContractContentsComposer = 2946;
    public const uint WiredContractUpdateResultComposer = 1061;

    // Wired monitor.
    public const uint WiredRoomStatsComposer = 324;
    public const uint WiredErrorLogsComposer = 1828;
    // Wired menu permissions, sent on room entry.
    public const uint WiredPermissionsComposer = 3963;
    // Answers WiredGetVariablesForObject.
    public const uint WiredVariablesForObjectComposer = 720;
    // Answers of the Variables tab's Highlight, Delete and Manage buttons.
    public const uint WiredAllVariableHoldersComposer = 2891;
    public const uint WiredUserVariablesListComposer = 697;
    public const uint WiredUserPermanentVariablesComposer = 1867;
    public const uint WiredSetUserPermanentVariableResultComposer = 2461;
    public const uint WiredMenuErrorComposer = 289;

    // Free product claims (cursed dragon adoption) and the new-feature promo expiry.
    public const uint HasClaimedProductResponseComposer = 3677;
    public const uint ClaimProductResultComposer = 43;
    public const uint SecondsUntilComposer = 325;
}
