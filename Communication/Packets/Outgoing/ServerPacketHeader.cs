namespace Plus.Communication.Packets.Outgoing;

public static class ServerPacketHeader
{
    // Handshake
    public const uint SimplePollStartComposer = 497;
    public const uint SimplePollAnswerComposer = 2592;
    public const uint SimplePollAnswersComposer = 2983;
    public const uint InitDiffieHandshakeComposer = 3371;
    public const uint SecretKeyComposer = 137;
    public const uint AuthenticationOkComposer = 2926;
    public const uint UserObjectComposer = 604;
    public const uint UserPerksComposer = 490;
    public const uint KickbackInfoComposer = 2791;
    public const uint UserRightsComposer = 833;
    public const uint GenericErrorComposer = 2243;
    public const uint DisconnectReasonComposer = 4000;
    public const uint SetUniqueIdComposer = 65511;
    public const uint AvailabilityStatusComposer = 2055;

    // Avatar
    public const uint WardrobeComposer = 3894;

    // Catalog
    public const uint CatalogIndexComposer = 3453;
    public const uint CatalogItemDiscountComposer = 3993;
    public const uint BuildersClubSubscriptionStatusComposer = 1910; // package_5/class_1773
    public const uint BuildersClubFurniCountComposer = 3928; // package_5/class_2247
    public const uint BuildersClubPlacementWarningComposer = 2075; // package_64/class_3565
    public const uint PurchaseOKComposer = 874;
    public const uint CatalogOfferComposer = 1684;
    public const uint CatalogPageComposer = 1260;
    public const uint CatalogUpdatedComposer = 2750;
    public const uint HabboClubOffersComposer = 2606;
    public const uint SellablePetBreedsComposer = 2532;
    public const uint GroupFurniConfigComposer = 3535;
    public const uint PresentDeliverErrorComposer = 3636;

    // Quests
    public const uint QuestListComposer = 1970;
    public const uint QuestCompletedComposer = 850;
    public const uint QuestAbortedComposer = 1273;
    public const uint QuestStartedComposer = 3239;

    // Room Avatar
    public const uint ActionComposer = 3882;
    public const uint SleepComposer = 11;
    public const uint DanceComposer = 3169;
    public const uint CarryObjectComposer = 3997;
    public const uint AvatarEffectComposer = 2517;

    // Room Chat
    public const uint ChatComposer = 1965;
    public const uint ShoutComposer = 294;
    public const uint WhisperComposer = 3601;
    public const uint FloodControlComposer = 1967;
    public const uint UserTypingComposer = 2032;

    // Room Engine
    public const uint UsersComposer = 2687;
    public const uint FurnitureAliasesComposer = 41;
    public const uint ObjectAddComposer = 960;
    public const uint ObjectsComposer = 1323;
    public const uint ObjectUpdateComposer = 3341;
    public const uint ObjectDataUpdateComposer = 3684;
    public const uint ObjectsDataUpdateComposer = 1330;
    public const uint FurniRecolorUpdateComposer = 345; // package_64/class_3789
    public const uint ObjectRemoveComposer = 3084;
    public const uint ObjectRemoveMultipleComposer = 3719; // package_64/class_3737
    public const uint SlideObjectBundleComposer = 114;
    public const uint ItemsComposer = 3000;
    public const uint ItemAddComposer = 3985;
    public const uint ItemUpdateComposer = 92;
    // Plus-only (custom, not an AIR id): furni metadata for the entries above. 5120 is unused on AIR WIN63-202610151745-318700621 (incoming)
    // and on the Volt-Renderer incoming table.
    public const uint FurnitureMetadataComposer = 5120;
    public const uint ItemRemoveComposer = 73;

    // Room Session
    public const uint RoomForwardComposer = 2059;
    public const uint RoomReadyComposer = 2725;
    public const uint OpenConnectionComposer = 369;
    public const uint CloseConnectionComposer = 2302;
    public const uint FlatAccessibleComposer = 3878;
    public const uint CantConnectComposer = 1293;

    // Room Permissions
    public const uint YouAreControllerComposer = 2045;
    public const uint YouAreNotControllerComposer = 2704;
    public const uint YouAreOwnerComposer = 3015;

    // Room Settings
    public const uint RoomSettingsDataComposer = 819;
    public const uint RoomSettingsSavedComposer = 3337;
    public const uint FlatControllerRemovedComposer = 151;
    public const uint FlatControllerAddedComposer = 2147;
    public const uint RoomRightsListComposer = 3482;

    // Room Furniture
    public const uint WiredValidationErrorComposer = 1324;
    public const uint WiredMonitorDataComposer = 5101;
    public const uint WiredRoomLogPageComposer = 3267;
    public const uint WiredRoomSettingsDataComposer = 5102;
    public const uint WiredRewardResultComposer = 2281;
    public const uint WiredMovementsComposer = 203;
    public const uint WiredFurniMoveStyleComposer = 5110;
    public const uint WiredClickSettingsComposer = 3814;
    public const uint WiredClickUserResponseComposer = 995;
    public const uint InClientLinkComposer = 480;
    public const uint WiredVariableFxConfigsComposer = 2589; // class_2605
    public const uint WiredVariableFxConfigsRemovedComposer = 3749; // class_3257
    public const uint WiredVariableFxStatusComposer = 447; // class_2753
    public const uint WiredVariableFxStatusRemovedComposer = 218; // class_2542
    public const uint WiredUserVariablesDataComposer = 5103;
    public const uint WiredAllVariablesHashComposer = 2277;
    public const uint WiredAllVariablesDiffComposer = 2015;
    public const uint WiredVariableHoldersPageComposer = 9461;
    public const uint WiredVariableHoldersComposer = 9462;
    public const uint HideWiredConfigComposer = 1441;
    public const uint WiredEffectConfigComposer = 2483;
    public const uint WiredSelectorConfigComposer = 1671;
    public const uint WiredAddonConfigComposer = 66;
    public const uint WiredVariableConfigComposer = 2337;
    public const uint WiredConditionConfigComposer = 1649;
    public const uint WiredTriggeRconfigComposer = 3691;
    public const uint MoodlightConfigComposer = 1664;
    public const uint GroupFurniSettingsComposer = 2325;
    public const uint OpenGiftComposer = 2186;

    // Navigator
    public const uint UpdateFavouriteRoomComposer = 2163;
    public const uint NavigatorLiftedRoomsComposer = 2689;
    public const uint NavigatorPreferencesComposer = 1618;
    public const uint NavigatorFlatCatsComposer = 1909;
    public const uint NavigatorMetaDataParserComposer = 1556;
    public const uint NavigatorCollapsedCategoriesComposer = 2857;

    // Messenger
    public const uint BuddyListComposer = 79;
    public const uint FriendRequestsComposer = 3672;
    public const uint NewBuddyRequestComposer = 3200;

    // Moderation
    public const uint ModeratorInitComposer = 1281;
    public const uint ModeratorUserRoomVisitsComposer = 1409;
    public const uint ModeratorRoomChatlogComposer = 3519;
    public const uint ModeratorUserInfoComposer = 50;
    public const uint ModeratorSupportTicketResponseComposer = 1844;
    public const uint ModeratorUserChatlogComposer = 2404;
    public const uint ModeratorRoomInfoComposer = 450;
    public const uint ModeratorSupportTicketComposer = 2875;
    public const uint ModeratorTicketChatlogComposer = 3608;
    public const uint CallForHelpPendingCallsComposer = 196;
    public const uint CfhTopicsInitComposer = 1529;

    // Inventory
    public const uint CreditBalanceComposer = 2409;
    public const uint BadgesComposer = 550;
    public const uint FurniListAddComposer = 3662;
    public const uint FurniListNotificationComposer = 949;
    public const uint FurniListRemoveComposer = 232;
    public const uint FurniListComposer = 1813;
    public const uint FurniListUpdateComposer = 500;
    public const uint AvatarEffectsComposer = 2586;
    public const uint AvatarEffectActivatedComposer = 516;
    public const uint AvatarEffectExpiredComposer = 1289;
    public const uint AvatarEffectAddedComposer = 2125;
    public const uint TradingErrorComposer = 3729;
    public const uint TradingAcceptComposer = 179;
    public const uint TradingStartComposer = 714;
    public const uint TradingUpdateComposer = 3923;
    public const uint TradingClosedComposer = 2376;
    public const uint TradingCompleteComposer = 2956;
    public const uint TradingFinishComposer = 3168;

    // Inventory Achievements
    public const uint AchievementsComposer = 493;
    public const uint AchievementScoreComposer = 2686;
    public const uint AchievementUnlockedComposer = 3060;
    public const uint AchievementProgressedComposer = 1504;

    // Notifications
    public const uint ActivityPointsComposer = 1652;
    public const uint HabboActivityPointNotificationComposer = 1010;

    // Users
    public const uint ScrSendUserInfoComposer = 1857;
    public const uint IgnoredUsersComposer = 2729;

    // Groups
    public const uint UnknownGroupComposer = 3823;
    public const uint GroupMembershipRequestedComposer = 2278;
    public const uint ManageGroupComposer = 3979;
    public const uint HabboGroupBadgesComposer = 272;
    public const uint NewGroupInfoComposer = 2947;
    public const uint GroupInfoComposer = 3630;
    public const uint GroupCreationWindowComposer = 102;
    public const uint SetGroupIdComposer = 1718;
    public const uint GroupMembersComposer = 1277;
    public const uint UpdateFavouriteGroupComposer = 2480;
    public const uint GroupMemberUpdatedComposer = 1325;
    public const uint GroupConfirmRemoveMemberComposer = 65505;
    public const uint GroupDeactivatedComposer = 1272;
    public const uint RefreshFavouriteGroupComposer = 1025;

    // Group Forums
    public const uint ForumsListDataComposer = 3418;
    public const uint ForumDataComposer = 781;
    public const uint ThreadCreatedComposer = 733;
    public const uint ThreadDataComposer = 124;
    public const uint ThreadsListDataComposer = 944;
    public const uint ThreadUpdatedComposer = 1475;
    public const uint ThreadReplyComposer = 867;
    public const uint ForumsUnreadCountComposer = 1304;

    // Sound
    public const uint SoundSettingsComposer = 1853;

    public const uint AvatarAspectUpdateComposer = 584;
    public const uint HelperToolComposer = 2127;
    public const uint RoomErrorNotifComposer = 3419;
    public const uint FollowFriendFailedComposer = 2454;

    public const uint FindFriendsProcessResultComposer = 2213;
    public const uint UserChangeComposer = 3625;
    public const uint FloorHeightMapComposer = 3757;
    public const uint RoomInfoUpdatedComposer = 2469;
    public const uint MessengerErrorComposer = 2274;
    public const uint MarketplaceCanMakeOfferResultComposer = 747;
    public const uint GameAccountStatusComposer = 1407;
    public const uint GuestRoomSearchResultComposer = 3586;
    public const uint NewUserExperienceGiftOfferComposer = 3396;
    public const uint UpdateUsernameComposer = 1976;
    public const uint VoucherRedeemOkComposer = 552;
    public const uint FigureSetIdsComposer = 3770;
    public const uint StickyNoteComposer = 1516;
    public const uint UserRemoveComposer = 2914;
    public const uint GetGuestRoomResultComposer = 1450;
    public const uint DoorbellComposer = 885;

    public const uint GiftWrappingConfigurationComposer = 377;
    public const uint GetRelationshipsComposer = 2706;
    public const uint FriendNotificationComposer = 2579;
    public const uint BadgeEditorPartsComposer = 1415;
    public const uint TraxSongInfoComposer = 3654;
    public const uint PostUpdatedComposer = 2677;
    public const uint UserUpdateComposer = 206;
    public const uint MutedComposer = 1306;
    public const uint MarketplaceConfigurationComposer = 1969;
    public const uint CheckGnomeNameComposer = 1396;
    public const uint OpenBotActionComposer = 3268;
    public const uint FavouritesComposer = 3316;
    public const uint TalentLevelUpComposer = 3576;

    public const uint UserTagsComposer = 65514;
    public const uint CampaignComposer = 226;
    public const uint RoomEventComposer = 1948;
    public const uint MarketplaceItemStatsComposer = 1878;
    public const uint HabboSearchResultComposer = 3083;
    public const uint PetHorseFigureInformationComposer = 3837;
    public const uint PetInventoryComposer = 298;
    public const uint PingComposer = 3107;
    public const uint RentableSpaceComposer = 1826;
    public const uint GetYouTubePlaylistComposer = 1492;
    public const uint RespectNotificationComposer = 3683;
    public const uint RecyclerPrizesComposer = 3512;
    public const uint RecyclerStatusComposer = 225;
    public const uint RecyclerFinishedComposer = 3533;
    public const uint GetRoomBannedUsersComposer = 98;
    public const uint RoomRatingComposer = 3867;
    public const uint PlayableGamesComposer = 65482;
    public const uint TalentTrackLevelComposer = 2218;
    public const uint JoinQueueComposer = 65499;
    public const uint MarketPlaceOwnOffersComposer = 3870;
    public const uint PetBreedingComposer = 3424;
    public const uint SubmitBullyReportComposer = 2314;
    public const uint UserNameChangeComposer = 171;
    public const uint FriendFurniStartConfirmationComposer = 1788;
    public const uint GuideReportingStatusComposer = 2198; // class_2139 [statusCode]
    public const uint VoucherRedeemErrorComposer = 2003;
    public const uint PurchaseErrorComposer = 2708;
    public const uint CampaignCalendarDoorOpenedComposer = 1461;
    public const uint FriendListUpdateComposer = 1914;

    public const uint UserFlatCatsComposer = 534;
    public const uint UpdateFreezeLivesComposer = 3179;
    public const uint UnbanUserFromRoomComposer = 3584;
    public const uint PetTrainingPanelComposer = 1597;
    public const uint FlatAccessDeniedComposer = 1764;
    public const uint LatencyPingResponseComposer = 3742;
    public const uint HabboUserBadgesComposer = 2014;
    public const uint HeightMapUpdateComposer = 65424;
    public const uint HeightMapComposer = 2099;

    public const uint CanCreateRoomComposer = 119;
    public const uint InstantMessageErrorComposer = 1465;
    public const uint GnomeBoxComposer = 2056;
    public const uint IgnoreStatusComposer = 2726;
    public const uint PetInformationComposer = 674;
    public const uint NavigatorSearchResultSetComposer = 1013;
    public const uint ConcurrentUsersGoalProgressComposer = 1690;
    public const uint VideoOffersRewardsComposer = 1140;
    public const uint SanctionStatusComposer = 1622;
    public const uint GetYouTubeVideoComposer = 2617;
    public const uint CheckPetNameComposer = 889;
    public const uint RespectPetNotificationComposer = 1375;
    public const uint EnforceCategoryUpdateComposer = 1658;
    public const uint CommunityGoalHallOfFameComposer = 487;
    public const uint RoomOccupiedTilesComposer = 1535;
    public const uint SendGameInvitationComposer = 65415;
    public const uint GiftWrappingErrorComposer = 2648;
    public const uint PromoArticlesComposer = 267;
    public const uint Game1WeeklyLeaderboardComposer = 65423;
    public const uint RentableSpacesErrorComposer = 986;
    public const uint AddExperiencePointsComposer = 589;
    public const uint GetRoomFilterListComposer = 2679;
    public const uint GameAchievementListComposer = 113;
    public const uint PromotableRoomsComposer = 3018;
    public const uint RoomEntryTileComposer = 2436;
    public const uint RoomEntryInfoComposer = 3478;
    public const uint RoomNotificationComposer = 3118;
    public const uint ClubGiftsComposer = 2555;
    public const uint MOTDNotificationComposer = 2601;
    public const uint PopularRoomTagsResultComposer = 165;
    public const uint NewConsoleMessageComposer = 1428;
    public const uint RoomPropertyComposer = 1734;
    public const uint MarketPlaceOffersComposer = 3388;
    public const uint TalentTrackComposer = 3430;
    public const uint ProfileInformationComposer = 3925;
    public const uint BadgeDefinitionsComposer = 2089;
    public const uint Game2WeeklyLeaderboardComposer = 687;
    public const uint NameChangeUpdateComposer = 1566;
    public const uint RoomVisualizationSettingsComposer = 2631;
    public const uint MarketplaceMakeOfferResultComposer = 1244;
    public const uint FlatCreatedComposer = 3694;
    public const uint BotInventoryComposer = 3392;
    public const uint LoadGameComposer = 65477;
    public const uint UpdateMagicTileComposer = 65486;
    public const uint MaintenanceStatusComposer = 1236;
    public const uint Game3WeeklyLeaderboardComposer = 669;
    public const uint GameListComposer = 65500;
    public const uint RoomMuteSettingsComposer = 147;
    public const uint RoomInviteComposer = 1265;
    public const uint FriendFurniOtherLockConfirmedComposer = 1201;
    public const uint FriendFurniCancelLockComposer = 65420;
    public const uint BroadcastMessageAlertComposer = 3280;
    public const uint MarketplaceCancelOfferResultComposer = 818;
    public const uint MarketplaceBuyOfferResultComposer = 618;
    public const uint NavigatorSettingsComposer = 2023;

    public const uint MessengerInitComposer = 2273;
    public const uint PollContentsComposer = 2765;
    public const uint PollOfferComposer = 2541;
    public const uint RoomUseHabbiconComposer = 3258;
    public const uint AllowedChatStylesComposer = 9340;
    public const uint CreatableRoomModelsComposer = 9341;
    public const uint UserHabbiconsComposer = 3581;
    public const uint UserHabbiconStatusChangedComposer = 2611;
    public const uint HabbiconShopDataComposer = 3792;
    public const uint HabbiconInfoComposer = 32;
    public const uint MessengerMessageAckComposer = 4902;
    public const uint MessengerMessageFailedComposer = 4903;
    public const uint MessengerMessageComposer = 4904;
    // Camera internal IDs; wire IDs are revision-specific.
    public const uint InitCameraComposer = 1530;
    public const uint CameraStorageUrlComposer = 3408;
    public const uint CameraPurchaseOKComposer = 358;
    public const uint CameraPublishStatusComposer = 1307;
    public const uint CompetitionStatusComposer = 1643;
    public const uint ThumbnailStatusComposer = 2304;
    public const uint RewardTracksComposer = 339;
    public const uint RewardTrackClaimResultComposer = 763;
    public const uint RewardTrackProgressComposer = 3631;
    public const uint RewardTrackPremiumPurchaseResultComposer = 1830;
    public const uint HabboClubExtendOfferComposer = 3176;
    public const uint ClubGiftReceivedComposer = 3400;
    public const uint PickMonthlyClubGiftComposer = 2835;
    // SnowStorm (AIR Game2 payloads); internal IDs match Volt's wire IDs except where noted.
    public const uint Game2GameDirectoryStatusComposer = 3525;
    public const uint Game2GameCreatedComposer = 5000;
    public const uint Game2InArenaQueueComposer = 2801;
    public const uint Game2GameLongDataComposer = 5002;
    public const uint Game2StartCounterComposer = 5003;
    public const uint Game2UserJoinedGameComposer = 5004;
    public const uint Game2UserLeftGameComposer = 1731;
    public const uint Game2StopCounterComposer = 317;
    public const uint Game2GameStartedComposer = 5009;
    public const uint Game2EnterArenaComposer = 5011;
    public const uint Game2ArenaEnteredComposer = 5013;
    public const uint Game2EnterArenaFailedComposer = 5014;
    public const uint Game2GameStatusComposer = 5015;
    public const uint Game2FullGameStatusComposer = 5016;
    public const uint Game2StageStartingComposer = 5017;
    public const uint Game2StageLoadComposer = 5018;
    public const uint Game2RejoinPreviousRoomComposer = 5019;
    public const uint Game2StageStillLoadingComposer = 5020;
    public const uint Game2GameEndingComposer = 5022;
    public const uint Game2GameChatComposer = 5023;
    public const uint Game2StageRunningComposer = 5024;
    public const uint Game2StageEndingComposer = 5025;
    public const uint Game2PlayerExitedGameArenaComposer = 5027;
    public const uint Game2PlayerRematchesComposer = 5029;
    public const uint Game2JoiningGameFailedComposer = 3304;
    public const uint Game2StartingGameFailedComposer = 363;
    public const uint Game2GameCancelledComposer = 3216;
    public const uint Game2GameNotFoundComposer = 65467;
    public const uint Game2UserBlockedComposer = 1158;
    public const uint Game2TotalLeaderboardComposer = 1583;
    public const uint Game2FriendsLeaderboardComposer = 1278;
    public const uint Game2WeeklyFriendsLeaderboardComposer = 1030;
    public const uint Game2TotalGroupLeaderboardComposer = 756;
    public const uint Game2WeeklyGroupLeaderboardComposer = 1950;
    public const uint SnowWarGameTokensComposer = 1572;
    public const uint CraftableProductsComposer = 3097;
    public const uint CraftingRecipeComposer = 210;
    public const uint CraftingResultComposer = 1760;
    public const uint CraftingRecipesAvailableComposer = 5;
    // Room music
    public const uint JukeboxPlaylistComposer = 1596;
    public const uint JukeboxPlaylistFullComposer = 906;
    public const uint NowPlayingComposer = 2443;
    public const uint OfficialSongIdComposer = 2915;
    public const uint SoundMachinePlaylistComposer = 1920;
    public const uint SongDisksInventoryComposer = 2999;
    public const uint QuizDataComposer = 1862;
    public const uint QuizResultsComposer = 655;
    public const uint CampaignCalendarDataComposer = 2860;
    public const uint WiredVariableInspectionDataComposer = 9483;
    public const uint WiredEnvironmentComposer = 3594;
    public const uint AreaHideComposer = 2373;
    public const uint ConfigurationItemStatesComposer = 3847;
    public const uint UserNftChatStylesComposer = 2233; // package_170/class_2869
    public const uint RaidProtectionCapabilityComposer = 3134; // package_236/class_4123
    public const uint WiredUserVariablesData64Composer = 9480;
    public const uint WiredVariableHolders64Composer = 9481;
    public const uint WiredVariableHoldersPage64Composer = 9482;
    // Wired chests and wired transactions; see reviews/chest-protocol-map.md.
    public const uint WiredChestOpenComposer = 126; // package_112.class_3078
    public const uint WiredChestSettingsUpdatedComposer = 1821; // package_112.class_2448
    public const uint WiredChestUpgradeComposer = 1164; // package_112.class_3165
    public const uint WiredChestCoinsComposer = 2097; // package_112.class_3811
    public const uint WiredChestFurniChunkComposer = 1130; // package_112.class_2622
    public const uint WiredChestFurniUpdateComposer = 53; // package_112.class_3301
    public const uint WiredTransactionFailComposer = 560; // package_196.class_3459
    public const uint WiredTransactionSuccessComposer = 1412; // package_196.class_3164
    public const uint WiredTradeErrorComposer = 546; // package_192.class_3902
    public const uint WiredChestTradeOpenComposer = 2921; // package_192.class_3307
    public const uint WiredChestTradeItemsComposer = 1494; // package_192.class_3032
    public const uint WiredChestTradeCancelledComposer = 1956; // package_192.class_3837
    public const uint WiredChestTradeCompletedComposer = 1771; // package_192.class_3876

    // October wired boxes (r88).
    public const uint WiredFurniTransparencyComposer = 1335; // class_3402
    public const uint WiredUserTransparencyComposer = 729; // class_2840
    public const uint WiredFurniHighlightComposer = 2675; // class_2692
    public const uint WiredWebApiKeyComposer = 1998; // class_3542
    public const uint WiredVariableLeaderboardSnapshotComposer = 2974; // package_160.class_3581
    public const uint WiredVariableLeaderboardChunkComposer = 63; // package_160.class_3180
    public const uint WiredLabelsComposer = 3690; // class_3055
    public const uint WiredLabelsUpdateComposer = 2591; // class_2868
    public const uint WiredLinkOpenComposer = 3648; // package_82.class_3704
    public const uint WiredLinkConfigurationComposer = 1714; // package_82.class_3204
    public const uint WiredLinkOperationResultComposer = 3261; // package_82.class_3734
    public const uint WiredLinkConnectionsComposer = 3320; // package_82.class_2754
    public const uint WiredLinkNetworkInvalidatedComposer = 716; // package_82.class_2368
    public const uint WiredUserHighlightComposer = 2094; // class_3615
    public const uint WiredFurniVisualizationStateComposer = 2803; // class_3572
    public const uint WiredUserVisualizationStateComposer = 3221; // class_3471
    public const uint ObjectRemoveConfirmComposer = 2784; // package_64/class_3467
    public const uint YouArePlayingGameComposer = 3915; // package_44/class_3268
    public const uint WiredContractOpenComposer = 1751; // package_99.class_3725
    public const uint WiredContractContentsComposer = 1261; // package_99.class_2410
    public const uint WiredContractUpdateResultComposer = 3567; // package_99.class_3013

    // Wired monitor.
    public const uint WiredRoomStatsComposer = 2006; // package_121.class_3190
    public const uint WiredErrorLogsComposer = 1345; // package_121.class_2831
    // Wired menu permissions, sent on room entry. Observed on AIR WIN63-202610131318-860371703 as package_121.class_3679, parser class_2713.
    public const uint WiredPermissionsComposer = 3966; // package_121.class_3691
    // Answers WiredGetVariablesForObject (package_121.class_3373, parser wiredmenu.class_4057).
    public const uint WiredVariablesForObjectComposer = 1478;
}
