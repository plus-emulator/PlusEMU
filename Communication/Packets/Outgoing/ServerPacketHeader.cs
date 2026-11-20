namespace Plus.Communication.Packets.Outgoing;

public static class ServerPacketHeader
{
    // Handshake
    public const uint SimplePollStartComposer = 3727;
    public const uint SimplePollAnswerComposer = 808;
    public const uint SimplePollAnswersComposer = 908;
    public const uint InitDiffieHandshakeComposer = 2970;
    public const uint SecretKeyComposer = 698;
    public const uint AuthenticationOkComposer = 2332;
    public const uint UserObjectComposer = 1703;
    public const uint UserPerksComposer = 721;
    public const uint KickbackInfoComposer = 1616;
    public const uint UserRightsComposer = 1539;
    public const uint GamePlayerValueComposer = 786;
    public const uint GenericErrorComposer = 2260;
    public const uint DisconnectReasonComposer = 4000;
    public const uint SetUniqueIdComposer = 65511;
    public const uint AvailabilityStatusComposer = 2087;

    // Avatar
    public const uint WardrobeComposer = 130;
    public const uint UserNftWardrobeComposer = 3428;
    public const uint SelectedNftWardrobeOutfitComposer = 3914;

    // Collectibles hub
    public const uint CollectibleWalletAddressesComposer = 3804;
    public const uint CollectorScoreComposer = 1520;
    public const uint NftCollectionsComposer = 2489;
    public const uint NftCollectionBonusClaimResultComposer = 3663;
    public const uint NftCollectionRewardClaimResultComposer = 2551;
    public const uint NftStoreOffersComposer = 3329;
    public const uint NftStorePurchaseResultComposer = 1305;
    public const uint CollectibleMintableItemTypesComposer = 1955;
    public const uint CollectibleMintingEnabledComposer = 270;
    public const uint MintCollectibleResultComposer = 3047;
    public const uint NftClaimsComposer = 1645;
    public const uint NftClaimResultComposer = 3082;
    public const uint EmeraldBalanceComposer = 2128;

    // Catalog
    public const uint CatalogIndexComposer = 825;
    public const uint CatalogItemDiscountComposer = 2310;
    public const uint BuildersClubSubscriptionStatusComposer = 2146;
    public const uint BuildersClubFurniCountComposer = 2828;
    public const uint BuildersClubPlacementWarningComposer = 1485;
    public const uint PurchaseOKComposer = 1926;
    public const uint CatalogOfferComposer = 3759;
    public const uint CatalogPageComposer = 3661;
    public const uint CatalogUpdatedComposer = 950;
    public const uint HabboClubOffersComposer = 3450;
    public const uint SellablePetBreedsComposer = 3354;
    public const uint GroupFurniConfigComposer = 967;
    public const uint PresentDeliverErrorComposer = 2848;

    // Quests
    public const uint QuestListComposer = 2707;
    public const uint QuestCompletedComposer = 606;
    public const uint QuestAbortedComposer = 930;
    public const uint QuestStartedComposer = 3972;

    // Room Avatar
    public const uint ActionComposer = 3209;
    public const uint SleepComposer = 1278;
    public const uint DanceComposer = 134;
    public const uint CarryObjectComposer = 3732;
    public const uint AvatarEffectComposer = 42;

    // Room Chat
    public const uint UserPurchasableChatStylesComposer = 2682;
    public const uint UserPurchasableChatStyleChangedComposer = 699;
    public const uint ChatComposer = 1429;
    public const uint ShoutComposer = 3621;
    public const uint WhisperComposer = 264;
    public const uint FloodControlComposer = 2077;
    public const uint UserTypingComposer = 2241;

    // Room Engine
    public const uint UsersComposer = 1245;
    public const uint FurnitureAliasesComposer = 2936;
    public const uint ObjectAddComposer = 2585;
    public const uint ObjectsComposer = 1202;
    public const uint ObjectUpdateComposer = 3342;
    public const uint ObjectDataUpdateComposer = 1279;
    public const uint ObjectsDataUpdateComposer = 2631;
    public const uint FurniRecolorUpdateComposer = 166;
    public const uint ObjectRemoveComposer = 2785;
    public const uint ObjectRemoveMultipleComposer = 1571;
    public const uint SlideObjectBundleComposer = 3213;
    public const uint ItemsComposer = 552;
    public const uint ItemAddComposer = 3917;
    public const uint ItemUpdateComposer = 1442;
    // Plus-only id, not an official one: furni metadata for the entries above. 5120 is free in the client's and Volt-Renderer's
    // incoming tables.
    public const uint FurnitureMetadataComposer = 5120;
    public const uint ItemRemoveComposer = 1326;

    // Room Session
    public const uint RoomForwardComposer = 1769;
    public const uint RoomReadyComposer = 2397;
    public const uint OpenConnectionComposer = 2883;
    public const uint CloseConnectionComposer = 3271;
    public const uint FlatAccessibleComposer = 804;
    public const uint CantConnectComposer = 1455;

    // Room Permissions
    public const uint YouAreControllerComposer = 2047;
    public const uint YouAreNotControllerComposer = 3425;
    public const uint YouAreOwnerComposer = 1083;

    // Room Settings
    public const uint RoomSettingsDataComposer = 3632;
    public const uint RoomSettingsSavedComposer = 2678;
    public const uint RoomSettingsSaveErrorComposer = 370;
    public const uint FlatControllerRemovedComposer = 1270;
    public const uint FlatControllerAddedComposer = 2801;
    public const uint RoomRightsListComposer = 2530;

    // Room Furniture
    public const uint WiredValidationErrorComposer = 3482;
    public const uint WiredMonitorDataComposer = 5101;
    public const uint WiredRoomLogPageComposer = 382;
    public const uint WiredRoomSettingsDataComposer = 5102;
    public const uint WiredRoomSettingsComposer = 151;
    public const uint WiredRewardResultComposer = 1468;
    public const uint WiredMovementsComposer = 163;
    public const uint WiredClickSettingsComposer = 1376;
    public const uint WiredClickUserResponseComposer = 1197;
    public const uint InClientLinkComposer = 3219;
    public const uint WiredVariableFxConfigsComposer = 2256;
    public const uint WiredVariableFxConfigsRemovedComposer = 1505;
    public const uint WiredVariableFxStatusComposer = 3749;
    public const uint WiredVariableFxStatusRemovedComposer = 3447;
    public const uint WiredUserVariablesDataComposer = 5103;
    public const uint WiredAllVariablesHashComposer = 431;
    public const uint WiredAllVariablesDiffComposer = 1774;
    public const uint WiredVariableHoldersPageComposer = 9461;
    public const uint WiredVariableHoldersComposer = 9462;
    public const uint HideWiredConfigComposer = 1679;
    public const uint WiredEffectConfigComposer = 118;
    public const uint WiredSelectorConfigComposer = 252;
    public const uint WiredAddonConfigComposer = 1767;
    public const uint WiredVariableConfigComposer = 2732;
    public const uint WiredConditionConfigComposer = 3637;
    public const uint WiredTriggeRconfigComposer = 1750;
    public const uint MoodlightConfigComposer = 2660;
    public const uint GroupFurniSettingsComposer = 1506;
    public const uint OpenGiftComposer = 999;

    // Navigator
    public const uint UpdateFavouriteRoomComposer = 2013;
    public const uint NavigatorLiftedRoomsComposer = 1678;
    public const uint NavigatorPreferencesComposer = 1082;
    public const uint NavigatorFlatCatsComposer = 3318;
    public const uint NavigatorMetaDataParserComposer = 2455;
    public const uint NavigatorCollapsedCategoriesComposer = 3600;

    // Messenger
    public const uint BuddyListComposer = 315;
    public const uint FriendRequestsComposer = 1965;
    public const uint NewBuddyRequestComposer = 2075;

    // Moderation
    public const uint ModeratorInitComposer = 500;
    public const uint ModeratorUserRoomVisitsComposer = 3731;
    public const uint ModeratorRoomChatlogComposer = 2812;
    public const uint ModeratorUserInfoComposer = 1719;
    public const uint ModeratorSupportTicketResponseComposer = 451;
    public const uint ModeratorUserChatlogComposer = 3429;
    public const uint ModeratorRoomInfoComposer = 2584;
    public const uint ModeratorSupportTicketComposer = 2485;
    public const uint ModeratorTicketChatlogComposer = 324;
    public const uint CallForHelpPendingCallsComposer = 1811;
    public const uint CfhTopicsInitComposer = 3492;

    // Inventory
    public const uint CreditBalanceComposer = 27;
    public const uint BadgesComposer = 2468;
    public const uint BadgeLeaderboardComposer = 60;
    public const uint FurniListAddComposer = 675;
    public const uint FurniListNotificationComposer = 1348;
    public const uint FurniListRemoveComposer = 216;
    public const uint FurniListRemoveMultipleComposer = 627;
    public const uint FurniListComposer = 2079;
    public const uint FurniListUpdateComposer = 72;
    public const uint AvatarEffectsComposer = 3563;
    public const uint AvatarEffectActivatedComposer = 1244;
    public const uint AvatarEffectExpiredComposer = 3717;
    public const uint AvatarEffectAddedComposer = 2869;
    public const uint TradingErrorComposer = 2918;
    public const uint TradingAcceptComposer = 2603;
    public const uint TradingStartComposer = 3913;
    public const uint TradingUpdateComposer = 1435;
    public const uint TradingClosedComposer = 3615;
    public const uint TradingCompleteComposer = 2323;
    public const uint TradingFinishComposer = 2302;

    // Inventory Achievements
    public const uint AchievementsComposer = 3005;
    public const uint AchievementScoreComposer = 653;
    public const uint AchievementUnlockedComposer = 1368;
    public const uint AchievementProgressedComposer = 1743;

    // Notifications
    public const uint ActivityPointsComposer = 1179;
    public const uint HabboActivityPointNotificationComposer = 3588;

    // Users
    public const uint ScrSendUserInfoComposer = 374;
    public const uint IgnoredUsersComposer = 3560;

    // Groups
    public const uint UnknownGroupComposer = 3798;
    public const uint GroupMembershipRequestedComposer = 2795;
    public const uint ManageGroupComposer = 1982;
    public const uint HabboGroupBadgesComposer = 1017;
    public const uint NewGroupInfoComposer = 989;
    public const uint GroupInfoComposer = 3670;
    public const uint GroupCreationWindowComposer = 1307;
    public const uint SetGroupIdComposer = 949;
    public const uint GroupMembersComposer = 3845;
    public const uint UpdateFavouriteGroupComposer = 591;
    public const uint GroupMemberUpdatedComposer = 2809;
    public const uint GroupConfirmRemoveMemberComposer = 65505;
    public const uint GroupDeactivatedComposer = 2185;
    public const uint RefreshFavouriteGroupComposer = 58;

    // Group Forums
    public const uint ForumsListDataComposer = 3860;
    public const uint ForumDataComposer = 3559;
    public const uint ThreadCreatedComposer = 3018;
    public const uint ThreadDataComposer = 3997;
    public const uint ThreadsListDataComposer = 2595;
    public const uint ThreadUpdatedComposer = 3523;
    public const uint ThreadReplyComposer = 3493;
    public const uint ForumsUnreadCountComposer = 1852;

    // Sound
    public const uint SoundSettingsComposer = 3635;

    public const uint AvatarAspectUpdateComposer = 1353;
    public const uint HelperToolComposer = 2934;
    public const uint RoomErrorNotifComposer = 2459;
    public const uint FollowFriendFailedComposer = 2754;

    public const uint FindFriendsProcessResultComposer = 3993;
    public const uint UserChangeComposer = 1318;
    public const uint FloorHeightMapComposer = 2354;
    public const uint RoomInfoUpdatedComposer = 532;
    public const uint MessengerErrorComposer = 2951;
    public const uint MarketplaceCanMakeOfferResultComposer = 2024;
    public const uint GameAccountStatusComposer = 3407;
    public const uint GuestRoomSearchResultComposer = 3873;
    public const uint NewUserExperienceGiftOfferComposer = 2199;
    public const uint UpdateUsernameComposer = 1712;
    public const uint VoucherRedeemOkComposer = 3674;
    public const uint FigureSetIdsComposer = 2755;
    public const uint StickyNoteComposer = 3851;
    public const uint UserRemoveComposer = 2824;
    public const uint GetGuestRoomResultComposer = 3821;
    public const uint DoorbellComposer = 700;

    public const uint GiftWrappingConfigurationComposer = 3592;
    public const uint GetRelationshipsComposer = 1945;
    public const uint FriendNotificationComposer = 3826;
    public const uint BadgeEditorPartsComposer = 1527;
    public const uint TraxSongInfoComposer = 3125;
    public const uint PostUpdatedComposer = 2324;
    public const uint UserUpdateComposer = 3064;
    public const uint MutedComposer = 2271;
    public const uint MarketplaceConfigurationComposer = 1038;
    public const uint CheckGnomeNameComposer = 693;
    public const uint OpenBotActionComposer = 2561;
    public const uint FavouritesComposer = 3955;
    public const uint TalentLevelUpComposer = 2521;

    public const uint UserTagsComposer = 65514;
    public const uint CampaignComposer = 191;
    public const uint BonusRareInfoComposer = 3728;
    public const uint RoomEventComposer = 2330;
    public const uint MarketplaceItemStatsComposer = 3353;
    public const uint HabboSearchResultComposer = 665;
    public const uint PetHorseFigureInformationComposer = 1371;
    public const uint PetInventoryComposer = 2445;
    public const uint PingComposer = 2857;
    public const uint RentableSpaceComposer = 785;
    public const uint GetYouTubePlaylistComposer = 2398;
    public const uint RespectNotificationComposer = 3166;
    public const uint RecyclerPrizesComposer = 492;
    public const uint RecyclerStatusComposer = 69;
    public const uint RecyclerFinishedComposer = 3361;
    public const uint GetRoomBannedUsersComposer = 2539;
    public const uint RoomRatingComposer = 3438;
    public const uint PlayableGamesComposer = 65482;
    public const uint TalentTrackLevelComposer = 1509;
    public const uint JoinQueueComposer = 65499;
    public const uint MarketPlaceOwnOffersComposer = 862;
    public const uint PetBreedingComposer = 253;
    public const uint GoToBreedingNestFailureComposer = 2246;
    public const uint SubmitBullyReportComposer = 3028;
    public const uint UserNameChangeComposer = 2759;
    public const uint FriendFurniStartConfirmationComposer = 3065;
    public const uint GuideReportingStatusComposer = 3974;
    public const uint VoucherRedeemErrorComposer = 1887;
    public const uint PurchaseErrorComposer = 1781;
    public const uint CampaignCalendarDoorOpenedComposer = 3848;
    public const uint FriendListUpdateComposer = 3597;

    public const uint UserFlatCatsComposer = 1417;
    public const uint UnbanUserFromRoomComposer = 3517;
    public const uint PetTrainingPanelComposer = 3216;
    public const uint PetAddedToInventoryComposer = 1004;
    public const uint PetRemovedFromInventoryComposer = 1702;
    public const uint PetLevelNotificationComposer = 3029;
    public const uint PetStatusUpdateComposer = 386;
    public const uint PetLevelUpdateComposer = 1635;
    public const uint PetBreedingResultComposer = 1792;
    public const uint FlatAccessDeniedComposer = 559;
    public const uint LatencyPingResponseComposer = 2919;
    public const uint HabboUserBadgesComposer = 1042;
    public const uint HeightMapUpdateComposer = 2106;
    public const uint HeightMapComposer = 1868;

    public const uint CanCreateRoomComposer = 608;
    public const uint InstantMessageErrorComposer = 3245;
    public const uint GnomeBoxComposer = 220;
    public const uint IgnoreStatusComposer = 3087;
    public const uint PetInformationComposer = 2258;
    public const uint NavigatorSearchResultSetComposer = 3497;
    public const uint ConcurrentUsersGoalProgressComposer = 3356;
    public const uint VideoOffersRewardsComposer = 2843;
    public const uint SanctionStatusComposer = 1624;
    public const uint GetYouTubeVideoComposer = 570;
    public const uint CheckPetNameComposer = 2449;
    public const uint RespectPetNotificationComposer = 874;
    public const uint EnforceCategoryUpdateComposer = 3330;
    public const uint CommunityGoalHallOfFameComposer = 1518;
    public const uint RoomOccupiedTilesComposer = 3739;
    public const uint SendGameInvitationComposer = 65415;
    public const uint GiftWrappingErrorComposer = 325;
    public const uint PromoArticlesComposer = 1453;
    public const uint Game1WeeklyLeaderboardComposer = 65423;
    public const uint RentableSpacesErrorComposer = 3605;
    public const uint AddExperiencePointsComposer = 1732;
    public const uint GetRoomFilterListComposer = 3286;
    public const uint GameAchievementListComposer = 100;
    public const uint PromotableRoomsComposer = 458;
    public const uint RoomEntryTileComposer = 430;
    public const uint RoomEntryInfoComposer = 3758;
    public const uint RoomNotificationComposer = 3155;
    public const uint ClubGiftsComposer = 1057;
    public const uint MOTDNotificationComposer = 2429;
    public const uint PopularRoomTagsResultComposer = 3192;
    public const uint NewConsoleMessageComposer = 3013;
    public const uint RoomPropertyComposer = 1825;
    public const uint MarketPlaceOffersComposer = 3254;
    public const uint TalentTrackComposer = 2838;
    public const uint ProfileInformationComposer = 643;
    public const uint BadgeDefinitionsComposer = 3270;
    public const uint Game2WeeklyLeaderboardComposer = 3898;
    public const uint NameChangeUpdateComposer = 3756;
    public const uint RoomVisualizationSettingsComposer = 2702;
    public const uint MarketplaceMakeOfferResultComposer = 1208;
    public const uint FlatCreatedComposer = 3664;
    public const uint BotInventoryComposer = 2568;
    public const uint LoadGameComposer = 65477;
    public const uint UpdateMagicTileComposer = 65486;
    public const uint MaintenanceStatusComposer = 2546;
    public const uint Game3WeeklyLeaderboardComposer = 738;
    public const uint GameListComposer = 65500;
    public const uint RoomMuteSettingsComposer = 3136;
    public const uint RoomInviteComposer = 2424;
    public const uint FriendFurniOtherLockConfirmedComposer = 2237;
    public const uint FriendFurniCancelLockComposer = 65420;
    public const uint BroadcastMessageAlertComposer = 3907;
    public const uint ModeratorMessageComposer = 1860;
    public const uint MarketplaceCancelOfferResultComposer = 3818;
    public const uint MarketplaceBuyOfferResultComposer = 2522;
    public const uint NavigatorSettingsComposer = 2849;

    public const uint MessengerInitComposer = 373;
    public const uint PollContentsComposer = 2727;
    public const uint PollOfferComposer = 1761;
    public const uint RoomUseHabbiconComposer = 2216;
    public const uint AllowedChatStylesComposer = 9340;
    public const uint CreatableRoomModelsComposer = 9341;
    public const uint UserHabbiconsComposer = 3473;
    public const uint UserHabbiconStatusChangedComposer = 3153;
    public const uint HabbiconShopDataComposer = 94;
    public const uint HabbiconInfoComposer = 1486;
    public const uint MessengerMessageAckComposer = 4902;
    public const uint MessengerMessageFailedComposer = 4903;
    public const uint MessengerMessageComposer = 4904;
    // Camera internal IDs; wire IDs are revision-specific.
    public const uint InitCameraComposer = 2272;
    public const uint CameraStorageUrlComposer = 344;
    public const uint CameraPurchaseOKComposer = 1812;
    public const uint CameraPublishStatusComposer = 305;
    public const uint CompetitionStatusComposer = 892;
    public const uint ThumbnailStatusComposer = 3655;
    public const uint RewardTracksComposer = 2142;
    public const uint RewardTrackClaimResultComposer = 816;
    public const uint RewardTrackProgressComposer = 792;
    public const uint RewardTrackPremiumPurchaseResultComposer = 1844;
    // Daily tasks.
    public const uint ActiveDailyTasksComposer = 2083;
    public const uint DailyTasksAddedComposer = 3778;
    public const uint DailyTaskUpdatedComposer = 1535;
    public const uint HabboClubExtendOfferComposer = 1565;
    public const uint ClubGiftReceivedComposer = 2859;
    public const uint PickMonthlyClubGiftComposer = 977;
    // SnowStorm (AIR Game2).
    public const uint Game2GameDirectoryStatusComposer = 70;
    public const uint Game2GameCreatedComposer = 3618;
    public const uint Game2InArenaQueueComposer = 3801;
    public const uint Game2GameLongDataComposer = 2633;
    public const uint Game2StartCounterComposer = 240;
    public const uint Game2UserJoinedGameComposer = 3566;
    public const uint Game2UserLeftGameComposer = 2118;
    public const uint Game2StopCounterComposer = 3538;
    public const uint Game2GameStartedComposer = 1579;
    public const uint Game2EnterArenaComposer = 1370;
    public const uint Game2ArenaEnteredComposer = 1335;
    public const uint Game2EnterArenaFailedComposer = 2912;
    public const uint Game2GameStatusComposer = 1932;
    public const uint Game2FullGameStatusComposer = 2413;
    public const uint Game2StageStartingComposer = 2821;
    public const uint Game2StageLoadComposer = 1234;
    public const uint Game2RejoinPreviousRoomComposer = 2576;
    public const uint Game2StageStillLoadingComposer = 2240;
    public const uint Game2GameEndingComposer = 1099;
    public const uint Game2GameChatComposer = 1821;
    public const uint Game2StageRunningComposer = 3743;
    public const uint Game2StageEndingComposer = 1077;
    public const uint Game2PlayerExitedGameArenaComposer = 52;
    public const uint Game2PlayerRematchesComposer = 562;
    public const uint Game2JoiningGameFailedComposer = 3080;
    public const uint Game2StartingGameFailedComposer = 1438;
    public const uint Game2GameCancelledComposer = 1113;
    public const uint Game2GameNotFoundComposer = 741;
    public const uint Game2UserBlockedComposer = 2730;
    public const uint Game2TotalLeaderboardComposer = 2766;
    public const uint Game2FriendsLeaderboardComposer = 1255;
    public const uint Game2WeeklyFriendsLeaderboardComposer = 1586;
    public const uint Game2TotalGroupLeaderboardComposer = 3301;
    public const uint Game2WeeklyGroupLeaderboardComposer = 632;
    public const uint SnowWarGameTokensComposer = 3198;
    public const uint CraftableProductsComposer = 1066;
    public const uint CraftingRecipeComposer = 1902;
    public const uint CraftingResultComposer = 3039;
    public const uint CraftingRecipesAvailableComposer = 663;
    // Room music
    public const uint JukeboxPlaylistComposer = 41;
    public const uint JukeboxPlaylistFullComposer = 1718;
    public const uint NowPlayingComposer = 21;
    public const uint OfficialSongIdComposer = 74;
    public const uint SoundMachinePlaylistComposer = 1015;
    public const uint SongDisksInventoryComposer = 2219;
    public const uint QuizDataComposer = 1807;
    public const uint QuizResultsComposer = 1176;
    public const uint CampaignCalendarDataComposer = 2412;
    public const uint WiredVariableInspectionDataComposer = 9483;
    public const uint WiredEnvironmentComposer = 951;
    public const uint AreaHideComposer = 1717;
    public const uint ConfigurationItemStatesComposer = 3397;
    public const uint UserNftChatStylesComposer = 3170;
    public const uint RaidProtectionCapabilityComposer = 2536;
    public const uint WiredUserVariablesData64Composer = 9480;
    public const uint WiredVariableHolders64Composer = 9481;
    public const uint WiredVariableHoldersPage64Composer = 9482;
    // Wired chests and wired transactions; see reviews/chest-protocol-map.md.
    public const uint WiredChestOpenComposer = 1350;
    public const uint WiredChestSettingsUpdatedComposer = 1401;
    public const uint WiredChestUpgradeComposer = 2773;
    public const uint WiredChestCoinsComposer = 2054;
    public const uint WiredChestFurniChunkComposer = 3562;
    public const uint WiredChestFurniUpdateComposer = 2991;
    public const uint WiredTransactionFailComposer = 3412;
    public const uint WiredTransactionSuccessComposer = 1320;
    public const uint WiredTradeErrorComposer = 391;
    public const uint WiredChestTradeOpenComposer = 1583;
    public const uint WiredChestTradeItemsComposer = 317;
    public const uint WiredChestTradeCancelledComposer = 2948;
    public const uint WiredChestTradeCompletedComposer = 1883;
    public const uint WiredTransactionLogListComposer = 1216;

    // October wired boxes.
    public const uint WiredFurniTransparencyComposer = 1875;
    public const uint WiredUserTransparencyComposer = 1339;
    public const uint WiredFurniHighlightComposer = 383;
    public const uint WiredWebApiKeyComposer = 338;
    public const uint WiredVariableLeaderboardSnapshotComposer = 3967;
    public const uint WiredVariableLeaderboardChunkComposer = 1373;
    public const uint WiredLabelsComposer = 2469;
    public const uint WiredLabelsUpdateComposer = 3925;
    public const uint WiredLinkOpenComposer = 2887;
    public const uint WiredLinkConfigurationComposer = 2003;
    public const uint WiredLinkOperationResultComposer = 3980;
    public const uint WiredLinkConnectionsComposer = 3931;
    public const uint WiredLinkNetworkInvalidatedComposer = 3668;
    public const uint WiredUserHighlightComposer = 622;
    public const uint WiredFurniVisualizationStateComposer = 1151;
    public const uint WiredUserVisualizationStateComposer = 1171;
    public const uint ObjectRemoveConfirmComposer = 234;
    public const uint YouArePlayingGameComposer = 1706;
    public const uint WiredContractOpenComposer = 3058;
    public const uint WiredContractContentsComposer = 3988;
    public const uint WiredContractUpdateResultComposer = 2316;

    // Wired monitor.
    public const uint WiredRoomStatsComposer = 1345;
    public const uint WiredErrorLogsComposer = 1426;
    // Wired menu permissions, sent on room entry.
    public const uint WiredPermissionsComposer = 2817;
    // Answers WiredGetVariablesForObject.
    public const uint WiredVariablesForObjectComposer = 3647;
    // Answers of the Variables tab's Highlight, Delete and Manage buttons.
    public const uint WiredAllVariableHoldersComposer = 595;
    public const uint WiredUserVariablesListComposer = 3533;
    public const uint WiredUserPermanentVariablesComposer = 2933;
    public const uint WiredSetUserPermanentVariableResultComposer = 2425;
    public const uint WiredMenuErrorComposer = 901;

    // Free product claims (cursed dragon adoption) and the new-feature promo expiry.
    public const uint HasClaimedProductResponseComposer = 713;
    public const uint ClaimProductResultComposer = 994;
    public const uint SecondsUntilComposer = 2845;
}
