namespace Plus.Communication.Packets.Outgoing;

public static class ServerPacketHeader
{
    // Handshake
    public const uint SimplePollStartComposer = 2113;
    public const uint SimplePollAnswerComposer = 3987;
    public const uint SimplePollAnswersComposer = 2781;
    public const uint InitDiffieHandshakeComposer = 589;
    public const uint SecretKeyComposer = 2136;
    public const uint AuthenticationOkComposer = 3097;
    public const uint UserObjectComposer = 2606;
    public const uint UserPerksComposer = 3519;
    public const uint KickbackInfoComposer = 3827;
    public const uint UserRightsComposer = 550;
    public const uint GenericErrorComposer = 1550;
    public const uint DisconnectReasonComposer = 4000;
    public const uint SetUniqueIdComposer = 65511;
    public const uint AvailabilityStatusComposer = 3147;

    // Avatar
    public const uint WardrobeComposer = 831;
    public const uint UserNftWardrobeComposer = 1345; // package_210/class_3851

    // Catalog
    public const uint CatalogIndexComposer = 1462;
    public const uint CatalogItemDiscountComposer = 853;
    public const uint BuildersClubSubscriptionStatusComposer = 228; // package_5/class_1773
    public const uint BuildersClubFurniCountComposer = 738; // package_5/class_2247
    public const uint BuildersClubPlacementWarningComposer = 3594; // package_64/class_3565
    public const uint PurchaseOKComposer = 2258;
    public const uint CatalogOfferComposer = 2985;
    public const uint CatalogPageComposer = 3143;
    public const uint CatalogUpdatedComposer = 1976;
    public const uint HabboClubOffersComposer = 948;
    public const uint SellablePetBreedsComposer = 10;
    public const uint GroupFurniConfigComposer = 3941;
    public const uint PresentDeliverErrorComposer = 89;

    // Quests
    public const uint QuestListComposer = 3315;
    public const uint QuestCompletedComposer = 348;
    public const uint QuestAbortedComposer = 3396;
    public const uint QuestStartedComposer = 1445;

    // Room Avatar
    public const uint ActionComposer = 301;
    public const uint SleepComposer = 66;
    public const uint DanceComposer = 3795;
    public const uint CarryObjectComposer = 3981;
    public const uint AvatarEffectComposer = 1103;

    // Room Chat
    public const uint ChatComposer = 3184;
    public const uint ShoutComposer = 443;
    public const uint WhisperComposer = 2011;
    public const uint FloodControlComposer = 2214;
    public const uint UserTypingComposer = 2795;

    // Room Engine
    public const uint UsersComposer = 3046;
    public const uint FurnitureAliasesComposer = 1130;
    public const uint ObjectAddComposer = 2382;
    public const uint ObjectsComposer = 2236;
    public const uint ObjectUpdateComposer = 3458;
    public const uint ObjectDataUpdateComposer = 1200;
    public const uint ObjectsDataUpdateComposer = 3151;
    public const uint FurniRecolorUpdateComposer = 2132; // package_64/class_3789
    public const uint ObjectRemoveComposer = 167;
    public const uint ObjectRemoveMultipleComposer = 2419; // package_64/class_3737
    public const uint SlideObjectBundleComposer = 1238;
    public const uint ItemsComposer = 797;
    public const uint ItemAddComposer = 1091;
    public const uint ItemUpdateComposer = 787;
    // Plus-only (custom, not an AIR id): furni metadata for the entries above. 5120 is unused on AIR WIN63-202610151745-318700621 (incoming)
    // and on the Volt-Renderer incoming table.
    public const uint FurnitureMetadataComposer = 5120;
    public const uint ItemRemoveComposer = 1352;

    // Room Session
    public const uint RoomForwardComposer = 428;
    public const uint RoomReadyComposer = 394;
    public const uint OpenConnectionComposer = 1259;
    public const uint CloseConnectionComposer = 1613;
    public const uint FlatAccessibleComposer = 536;
    public const uint CantConnectComposer = 1429;

    // Room Permissions
    public const uint YouAreControllerComposer = 2872;
    public const uint YouAreNotControllerComposer = 1760;
    public const uint YouAreOwnerComposer = 3070;

    // Room Settings
    public const uint RoomSettingsDataComposer = 3865;
    public const uint RoomSettingsSavedComposer = 2865;
    public const uint FlatControllerRemovedComposer = 3759;
    public const uint FlatControllerAddedComposer = 3844;
    public const uint RoomRightsListComposer = 1076;

    // Room Furniture
    public const uint WiredValidationErrorComposer = 2248;
    public const uint WiredMonitorDataComposer = 5101;
    public const uint WiredRoomLogPageComposer = 976;
    public const uint WiredRoomSettingsDataComposer = 5102;
    public const uint WiredRoomSettingsComposer = 2114;
    public const uint WiredRewardResultComposer = 1353;
    public const uint WiredMovementsComposer = 1695;
    public const uint WiredFurniMoveStyleComposer = 5110;
    public const uint WiredClickSettingsComposer = 1494;
    public const uint WiredClickUserResponseComposer = 3678;
    public const uint InClientLinkComposer = 1931;
    public const uint WiredVariableFxConfigsComposer = 739; // class_2605
    public const uint WiredVariableFxConfigsRemovedComposer = 2588; // class_3257
    public const uint WiredVariableFxStatusComposer = 754; // class_2753
    public const uint WiredVariableFxStatusRemovedComposer = 236; // class_2542
    public const uint WiredUserVariablesDataComposer = 5103;
    public const uint WiredAllVariablesHashComposer = 3811;
    public const uint WiredAllVariablesDiffComposer = 908;
    public const uint WiredVariableHoldersPageComposer = 9461;
    public const uint WiredVariableHoldersComposer = 9462;
    public const uint HideWiredConfigComposer = 1403;
    public const uint WiredEffectConfigComposer = 743;
    public const uint WiredSelectorConfigComposer = 163;
    public const uint WiredAddonConfigComposer = 841;
    public const uint WiredVariableConfigComposer = 2629;
    public const uint WiredConditionConfigComposer = 1187;
    public const uint WiredTriggeRconfigComposer = 3485;
    public const uint MoodlightConfigComposer = 1372;
    public const uint GroupFurniSettingsComposer = 3101;
    public const uint OpenGiftComposer = 2120;

    // Navigator
    public const uint UpdateFavouriteRoomComposer = 2260;
    public const uint NavigatorLiftedRoomsComposer = 3288;
    public const uint NavigatorPreferencesComposer = 1446;
    public const uint NavigatorFlatCatsComposer = 3692;
    public const uint NavigatorMetaDataParserComposer = 3817;
    public const uint NavigatorCollapsedCategoriesComposer = 3493;

    // Messenger
    public const uint BuddyListComposer = 3783;
    public const uint FriendRequestsComposer = 693;
    public const uint NewBuddyRequestComposer = 843;

    // Moderation
    public const uint ModeratorInitComposer = 3454;
    public const uint ModeratorUserRoomVisitsComposer = 486;
    public const uint ModeratorRoomChatlogComposer = 102;
    public const uint ModeratorUserInfoComposer = 1021;
    public const uint ModeratorSupportTicketResponseComposer = 2878;
    public const uint ModeratorUserChatlogComposer = 2200;
    public const uint ModeratorRoomInfoComposer = 2167;
    public const uint ModeratorSupportTicketComposer = 838;
    public const uint ModeratorTicketChatlogComposer = 1113;
    public const uint CallForHelpPendingCallsComposer = 534;
    public const uint CfhTopicsInitComposer = 3266;

    // Inventory
    public const uint CreditBalanceComposer = 199;
    public const uint BadgesComposer = 1929;
    public const uint FurniListAddComposer = 2035;
    public const uint FurniListNotificationComposer = 586;
    public const uint FurniListRemoveComposer = 3550;
    public const uint FurniListComposer = 2017;
    public const uint FurniListUpdateComposer = 2892;
    public const uint AvatarEffectsComposer = 3450;
    public const uint AvatarEffectActivatedComposer = 2053;
    public const uint AvatarEffectExpiredComposer = 989;
    public const uint AvatarEffectAddedComposer = 2451;
    public const uint TradingErrorComposer = 1829;
    public const uint TradingAcceptComposer = 1481;
    public const uint TradingStartComposer = 1422;
    public const uint TradingUpdateComposer = 3354;
    public const uint TradingClosedComposer = 637;
    public const uint TradingCompleteComposer = 3952;
    public const uint TradingFinishComposer = 3675;

    // Inventory Achievements
    public const uint AchievementsComposer = 2930;
    public const uint AchievementScoreComposer = 730;
    public const uint AchievementUnlockedComposer = 147;
    public const uint AchievementProgressedComposer = 2978;

    // Notifications
    public const uint ActivityPointsComposer = 3861;
    public const uint HabboActivityPointNotificationComposer = 3443;

    // Users
    public const uint ScrSendUserInfoComposer = 3822;
    public const uint IgnoredUsersComposer = 985;

    // Groups
    public const uint UnknownGroupComposer = 2313;
    public const uint GroupMembershipRequestedComposer = 3651;
    public const uint ManageGroupComposer = 1779;
    public const uint HabboGroupBadgesComposer = 3856;
    public const uint NewGroupInfoComposer = 1102;
    public const uint GroupInfoComposer = 3326;
    public const uint GroupCreationWindowComposer = 2581;
    public const uint SetGroupIdComposer = 2830;
    public const uint GroupMembersComposer = 1315;
    public const uint UpdateFavouriteGroupComposer = 1792;
    public const uint GroupMemberUpdatedComposer = 3032;
    public const uint GroupConfirmRemoveMemberComposer = 65505;
    public const uint GroupDeactivatedComposer = 1681;
    public const uint RefreshFavouriteGroupComposer = 3556;

    // Group Forums
    public const uint ForumsListDataComposer = 1086;
    public const uint ForumDataComposer = 3616;
    public const uint ThreadCreatedComposer = 1510;
    public const uint ThreadDataComposer = 2988;
    public const uint ThreadsListDataComposer = 1844;
    public const uint ThreadUpdatedComposer = 1873;
    public const uint ThreadReplyComposer = 801;
    public const uint ForumsUnreadCountComposer = 3996;

    // Sound
    public const uint SoundSettingsComposer = 3400;

    public const uint AvatarAspectUpdateComposer = 944;
    public const uint HelperToolComposer = 2091;
    public const uint RoomErrorNotifComposer = 2696;
    public const uint FollowFriendFailedComposer = 977;

    public const uint FindFriendsProcessResultComposer = 1375;
    public const uint UserChangeComposer = 192;
    public const uint FloorHeightMapComposer = 3970;
    public const uint RoomInfoUpdatedComposer = 2866;
    public const uint MessengerErrorComposer = 1288;
    public const uint MarketplaceCanMakeOfferResultComposer = 2472;
    public const uint GameAccountStatusComposer = 2570;
    public const uint GuestRoomSearchResultComposer = 36;
    public const uint NewUserExperienceGiftOfferComposer = 2358;
    public const uint UpdateUsernameComposer = 1524;
    public const uint VoucherRedeemOkComposer = 492;
    public const uint FigureSetIdsComposer = 628;
    public const uint StickyNoteComposer = 3028;
    public const uint UserRemoveComposer = 1496;
    public const uint GetGuestRoomResultComposer = 1062;
    public const uint DoorbellComposer = 953;

    public const uint GiftWrappingConfigurationComposer = 2539;
    public const uint GetRelationshipsComposer = 2745;
    public const uint FriendNotificationComposer = 1880;
    public const uint BadgeEditorPartsComposer = 2610;
    public const uint TraxSongInfoComposer = 274;
    public const uint PostUpdatedComposer = 3074;
    public const uint UserUpdateComposer = 2254;
    public const uint MutedComposer = 1074;
    public const uint MarketplaceConfigurationComposer = 3368;
    public const uint CheckGnomeNameComposer = 69;
    public const uint OpenBotActionComposer = 1828;
    public const uint FavouritesComposer = 47;
    public const uint TalentLevelUpComposer = 3438;

    public const uint UserTagsComposer = 65514;
    public const uint CampaignComposer = 2370;
    public const uint RoomEventComposer = 2586;
    public const uint MarketplaceItemStatsComposer = 3815;
    public const uint HabboSearchResultComposer = 62;
    public const uint PetHorseFigureInformationComposer = 1415;
    public const uint PetInventoryComposer = 225;
    public const uint PingComposer = 3852;
    public const uint RentableSpaceComposer = 3152;
    public const uint GetYouTubePlaylistComposer = 2580;
    public const uint RespectNotificationComposer = 1421;
    public const uint RecyclerPrizesComposer = 3618;
    public const uint RecyclerStatusComposer = 1503;
    public const uint RecyclerFinishedComposer = 2137;
    public const uint GetRoomBannedUsersComposer = 2811;
    public const uint RoomRatingComposer = 324;
    public const uint PlayableGamesComposer = 65482;
    public const uint TalentTrackLevelComposer = 3781;
    public const uint JoinQueueComposer = 65499;
    public const uint MarketPlaceOwnOffersComposer = 1721;
    public const uint PetBreedingComposer = 943;
    public const uint SubmitBullyReportComposer = 1630;
    public const uint UserNameChangeComposer = 271;
    public const uint FriendFurniStartConfirmationComposer = 2166;
    public const uint GuideReportingStatusComposer = 721; // class_2139 [statusCode]
    public const uint VoucherRedeemErrorComposer = 83;
    public const uint PurchaseErrorComposer = 1115;
    public const uint CampaignCalendarDoorOpenedComposer = 136;
    public const uint FriendListUpdateComposer = 3035;

    public const uint UserFlatCatsComposer = 1279;
    public const uint UpdateFreezeLivesComposer = 425;
    public const uint UnbanUserFromRoomComposer = 752;
    public const uint PetTrainingPanelComposer = 1262;
    public const uint FlatAccessDeniedComposer = 2059;
    public const uint LatencyPingResponseComposer = 3179;
    public const uint HabboUserBadgesComposer = 1460;
    public const uint HeightMapUpdateComposer = 3841; // package_71/class_3258
    public const uint HeightMapComposer = 3698;

    public const uint CanCreateRoomComposer = 3897;
    public const uint InstantMessageErrorComposer = 2192;
    public const uint GnomeBoxComposer = 299;
    public const uint IgnoreStatusComposer = 1883;
    public const uint PetInformationComposer = 1652;
    public const uint NavigatorSearchResultSetComposer = 3157;
    public const uint ConcurrentUsersGoalProgressComposer = 3992;
    public const uint VideoOffersRewardsComposer = 1040;
    public const uint SanctionStatusComposer = 2307;
    public const uint GetYouTubeVideoComposer = 418;
    public const uint CheckPetNameComposer = 1628;
    public const uint RespectPetNotificationComposer = 2875;
    public const uint EnforceCategoryUpdateComposer = 3374;
    public const uint CommunityGoalHallOfFameComposer = 364;
    public const uint RoomOccupiedTilesComposer = 2193;
    public const uint SendGameInvitationComposer = 65415;
    public const uint GiftWrappingErrorComposer = 1316;
    public const uint PromoArticlesComposer = 3838;
    public const uint Game1WeeklyLeaderboardComposer = 65423;
    public const uint RentableSpacesErrorComposer = 2511;
    public const uint AddExperiencePointsComposer = 196;
    public const uint GetRoomFilterListComposer = 2645;
    public const uint GameAchievementListComposer = 202;
    public const uint PromotableRoomsComposer = 2043;
    public const uint RoomEntryTileComposer = 2498;
    public const uint RoomEntryInfoComposer = 553;
    public const uint RoomNotificationComposer = 180;
    public const uint ClubGiftsComposer = 1713;
    public const uint MOTDNotificationComposer = 3791;
    public const uint PopularRoomTagsResultComposer = 1256;
    public const uint NewConsoleMessageComposer = 1591;
    public const uint RoomPropertyComposer = 3255;
    public const uint MarketPlaceOffersComposer = 1361;
    public const uint TalentTrackComposer = 3079;
    public const uint ProfileInformationComposer = 3182;
    public const uint BadgeDefinitionsComposer = 2772;
    public const uint Game2WeeklyLeaderboardComposer = 2501;
    public const uint NameChangeUpdateComposer = 2798;
    public const uint RoomVisualizationSettingsComposer = 360;
    public const uint MarketplaceMakeOfferResultComposer = 367;
    public const uint FlatCreatedComposer = 295;
    public const uint BotInventoryComposer = 3309;
    public const uint LoadGameComposer = 65477;
    public const uint UpdateMagicTileComposer = 65486;
    public const uint MaintenanceStatusComposer = 2378;
    public const uint Game3WeeklyLeaderboardComposer = 244;
    public const uint GameListComposer = 65500;
    public const uint RoomMuteSettingsComposer = 2229;
    public const uint RoomInviteComposer = 3420;
    public const uint FriendFurniOtherLockConfirmedComposer = 1367;
    public const uint FriendFurniCancelLockComposer = 65420;
    public const uint BroadcastMessageAlertComposer = 1090;
    public const uint MarketplaceCancelOfferResultComposer = 848;
    public const uint MarketplaceBuyOfferResultComposer = 3989;
    public const uint NavigatorSettingsComposer = 2262;

    public const uint MessengerInitComposer = 2615;
    public const uint PollContentsComposer = 29;
    public const uint PollOfferComposer = 2725;
    public const uint RoomUseHabbiconComposer = 3408;
    public const uint AllowedChatStylesComposer = 9340;
    public const uint CreatableRoomModelsComposer = 9341;
    public const uint UserHabbiconsComposer = 1984;
    public const uint UserHabbiconStatusChangedComposer = 511;
    public const uint HabbiconShopDataComposer = 2095;
    public const uint HabbiconInfoComposer = 3330;
    public const uint MessengerMessageAckComposer = 4902;
    public const uint MessengerMessageFailedComposer = 4903;
    public const uint MessengerMessageComposer = 4904;
    // Camera internal IDs; wire IDs are revision-specific.
    public const uint InitCameraComposer = 2471;
    public const uint CameraStorageUrlComposer = 1515;
    public const uint CameraPurchaseOKComposer = 2718;
    public const uint CameraPublishStatusComposer = 2052;
    public const uint CompetitionStatusComposer = 2731;
    public const uint ThumbnailStatusComposer = 3659;
    public const uint RewardTracksComposer = 3598;
    public const uint RewardTrackClaimResultComposer = 3872;
    public const uint RewardTrackProgressComposer = 3578;
    public const uint RewardTrackPremiumPurchaseResultComposer = 1980;
    // Daily tasks, AIR WIN63-202610151745-318700621 (quest/dailytasks/DailyTasksController.as).
    public const uint ActiveDailyTasksComposer = 1617; // package_162.class_3100, parser package_180.class_3130
    public const uint DailyTasksAddedComposer = 3362; // package_162.class_2799, parser package_180.class_3531
    public const uint DailyTaskUpdatedComposer = 1799; // package_162.class_3370, parser package_180.class_3495
    public const uint HabboClubExtendOfferComposer = 2993;
    public const uint ClubGiftReceivedComposer = 1249;
    public const uint PickMonthlyClubGiftComposer = 3405;
    // SnowStorm (AIR Game2 payloads); internal IDs match Volt's wire IDs except where noted.
    public const uint Game2GameDirectoryStatusComposer = 915;
    public const uint Game2GameCreatedComposer = 5000;
    public const uint Game2InArenaQueueComposer = 108;
    public const uint Game2GameLongDataComposer = 5002;
    public const uint Game2StartCounterComposer = 5003;
    public const uint Game2UserJoinedGameComposer = 5004;
    public const uint Game2UserLeftGameComposer = 3976;
    public const uint Game2StopCounterComposer = 1813;
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
    public const uint Game2JoiningGameFailedComposer = 1966;
    public const uint Game2StartingGameFailedComposer = 3292;
    public const uint Game2GameCancelledComposer = 1346;
    public const uint Game2GameNotFoundComposer = 65467;
    public const uint Game2UserBlockedComposer = 817;
    public const uint Game2TotalLeaderboardComposer = 972;
    public const uint Game2FriendsLeaderboardComposer = 3486;
    public const uint Game2WeeklyFriendsLeaderboardComposer = 94;
    public const uint Game2TotalGroupLeaderboardComposer = 3459;
    public const uint Game2WeeklyGroupLeaderboardComposer = 1465;
    public const uint SnowWarGameTokensComposer = 1547;
    public const uint CraftableProductsComposer = 2701;
    public const uint CraftingRecipeComposer = 2812;
    public const uint CraftingResultComposer = 538;
    public const uint CraftingRecipesAvailableComposer = 2139;
    // Room music
    public const uint JukeboxPlaylistComposer = 630;
    public const uint JukeboxPlaylistFullComposer = 1741;
    public const uint NowPlayingComposer = 114;
    public const uint OfficialSongIdComposer = 277;
    public const uint SoundMachinePlaylistComposer = 372;
    public const uint SongDisksInventoryComposer = 1340;
    public const uint QuizDataComposer = 56;
    public const uint QuizResultsComposer = 382;
    public const uint CampaignCalendarDataComposer = 3186;
    public const uint WiredVariableInspectionDataComposer = 9483;
    public const uint WiredEnvironmentComposer = 3025;
    public const uint AreaHideComposer = 2763;
    public const uint ConfigurationItemStatesComposer = 3946;
    public const uint UserNftChatStylesComposer = 3026; // package_170/class_2869
    public const uint RaidProtectionCapabilityComposer = 554; // package_236/class_4123
    public const uint WiredUserVariablesData64Composer = 9480;
    public const uint WiredVariableHolders64Composer = 9481;
    public const uint WiredVariableHoldersPage64Composer = 9482;
    // Wired chests and wired transactions; see reviews/chest-protocol-map.md.
    public const uint WiredChestOpenComposer = 2598; // package_112.class_3078
    public const uint WiredChestSettingsUpdatedComposer = 2149; // package_112.class_2448
    public const uint WiredChestUpgradeComposer = 3314; // package_112.class_3165
    public const uint WiredChestCoinsComposer = 3978; // package_112.class_3811
    public const uint WiredChestFurniChunkComposer = 3704; // package_112.class_2622
    public const uint WiredChestFurniUpdateComposer = 3731; // package_112.class_3301
    public const uint WiredTransactionFailComposer = 2729; // package_196.class_3459
    public const uint WiredTransactionSuccessComposer = 470; // package_196.class_3164
    public const uint WiredTradeErrorComposer = 1961; // package_192.class_3902
    public const uint WiredChestTradeOpenComposer = 2881; // package_192.class_3307
    public const uint WiredChestTradeItemsComposer = 1716; // package_192.class_3032
    public const uint WiredChestTradeCancelledComposer = 3834; // package_192.class_3837
    public const uint WiredChestTradeCompletedComposer = 2367; // package_192.class_3876

    // October wired boxes (r88).
    public const uint WiredFurniTransparencyComposer = 3331; // class_3402
    public const uint WiredUserTransparencyComposer = 1479; // class_2840
    public const uint WiredFurniHighlightComposer = 2122; // class_2692
    public const uint WiredWebApiKeyComposer = 3954; // class_3542
    public const uint WiredVariableLeaderboardSnapshotComposer = 3516; // package_160.class_3581
    public const uint WiredVariableLeaderboardChunkComposer = 3948; // package_160.class_3180
    public const uint WiredLabelsComposer = 1142; // class_3055
    public const uint WiredLabelsUpdateComposer = 925; // class_2868
    public const uint WiredLinkOpenComposer = 2374; // package_82.class_3704
    public const uint WiredLinkConfigurationComposer = 1199; // package_82.class_3204
    public const uint WiredLinkOperationResultComposer = 2156; // package_82.class_3734
    public const uint WiredLinkConnectionsComposer = 2593; // package_82.class_2754
    public const uint WiredLinkNetworkInvalidatedComposer = 3715; // package_82.class_2368
    public const uint WiredUserHighlightComposer = 2235; // class_3615
    public const uint WiredFurniVisualizationStateComposer = 2207; // class_3572
    public const uint WiredUserVisualizationStateComposer = 1063; // class_3471
    public const uint ObjectRemoveConfirmComposer = 3589; // package_64/class_3467
    public const uint YouArePlayingGameComposer = 1784; // package_44/class_3268
    public const uint WiredContractOpenComposer = 3218; // package_99.class_3725
    public const uint WiredContractContentsComposer = 2309; // package_99.class_2410
    public const uint WiredContractUpdateResultComposer = 1085; // package_99.class_3013

    // Wired monitor.
    public const uint WiredRoomStatsComposer = 2746; // package_121.class_3190
    public const uint WiredErrorLogsComposer = 353; // package_121.class_2831
    // Wired menu permissions, sent on room entry. Observed on AIR WIN63-202610131318-860371703 as package_121.class_3679, parser class_2713.
    public const uint WiredPermissionsComposer = 2131; // package_121.class_3691
    // Answers WiredGetVariablesForObject (package_121.class_3373, parser wiredmenu.class_4057).
    public const uint WiredVariablesForObjectComposer = 2938;

    // Free product claims (cursed dragon adoption) and the new-feature promo expiry, AIR WIN63-202610151745-318700621.
    public const uint HasClaimedProductResponseComposer = 1215; // parser package_174.class_2885
    public const uint ClaimProductResultComposer = 3886; // parser package_174.class_3882
    public const uint SecondsUntilComposer = 1309; // parser package_205.class_3987
}
