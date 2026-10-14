namespace Plus.Communication.Packets.Outgoing;

public static class ServerPacketHeader
{
    // Handshake
    public const uint SimplePollStartComposer = 1578;
    public const uint SimplePollAnswerComposer = 3201;
    public const uint SimplePollAnswersComposer = 1797;
    public const uint InitDiffieHandshakeComposer = 2223;
    public const uint SecretKeyComposer = 2229;
    public const uint AuthenticationOkComposer = 1015;
    public const uint UserObjectComposer = 2715;
    public const uint UserPerksComposer = 1075;
    public const uint KickbackInfoComposer = 2543;
    public const uint UserRightsComposer = 2240;
    public const uint GenericErrorComposer = 2742;
    public const uint DisconnectReasonComposer = 4000;
    public const uint SetUniqueIdComposer = 65511;
    public const uint AvailabilityStatusComposer = 190;

    // Avatar
    public const uint WardrobeComposer = 369;

    // Catalog
    public const uint CatalogIndexComposer = 1196;
    public const uint CatalogItemDiscountComposer = 3991;
    public const uint PurchaseOKComposer = 3752;
    public const uint CatalogOfferComposer = 2707;
    public const uint CatalogPageComposer = 2540;
    public const uint CatalogUpdatedComposer = 983;
    public const uint HabboClubOffersComposer = 969;
    public const uint SellablePetBreedsComposer = 2765;
    public const uint GroupFurniConfigComposer = 3934;
    public const uint PresentDeliverErrorComposer = 3512;

    // Quests
    public const uint QuestListComposer = 650;
    public const uint QuestCompletedComposer = 699;
    public const uint QuestAbortedComposer = 3134;
    public const uint QuestStartedComposer = 166;

    // Room Avatar
    public const uint ActionComposer = 1067;
    public const uint SleepComposer = 2417;
    public const uint DanceComposer = 3871;
    public const uint CarryObjectComposer = 1003;
    public const uint AvatarEffectComposer = 943;

    // Room Chat
    public const uint ChatComposer = 1558;
    public const uint ShoutComposer = 2156;
    public const uint WhisperComposer = 3995;
    public const uint FloodControlComposer = 1223;
    public const uint UserTypingComposer = 1444;

    // Room Engine
    public const uint UsersComposer = 324;
    public const uint FurnitureAliasesComposer = 2319;
    public const uint ObjectAddComposer = 559;
    public const uint ObjectsComposer = 1354;
    public const uint ObjectUpdateComposer = 2875;
    public const uint ObjectDataUpdateComposer = 1456;
    public const uint ObjectsDataUpdateComposer = 80;
    public const uint ObjectRemoveComposer = 996;
    public const uint SlideObjectBundleComposer = 1505;
    public const uint ItemsComposer = 3242;
    public const uint ItemAddComposer = 3363;
    public const uint ItemUpdateComposer = 1436;
    public const uint ItemRemoveComposer = 382;

    // Room Session
    public const uint RoomForwardComposer = 281;
    public const uint RoomReadyComposer = 3733;
    public const uint OpenConnectionComposer = 2349;
    public const uint CloseConnectionComposer = 1729;
    public const uint FlatAccessibleComposer = 432;
    public const uint CantConnectComposer = 3413;

    // Room Permissions
    public const uint YouAreControllerComposer = 399;
    public const uint YouAreNotControllerComposer = 3085;
    public const uint YouAreOwnerComposer = 1428;

    // Room Settings
    public const uint RoomSettingsDataComposer = 2376;
    public const uint RoomSettingsSavedComposer = 3840;
    public const uint FlatControllerRemovedComposer = 1207;
    public const uint FlatControllerAddedComposer = 339;
    public const uint RoomRightsListComposer = 1146;

    // Room Furniture
    public const uint WiredValidationErrorComposer = 2091;
    public const uint WiredMonitorDataComposer = 5101;
    public const uint WiredRoomLogPageComposer = 1028;
    public const uint WiredRoomSettingsDataComposer = 5102;
    public const uint WiredRewardResultComposer = 3662;
    public const uint WiredMovementsComposer = 2503;
    public const uint WiredFurniMoveStyleComposer = 5110;
    public const uint WiredClickSettingsComposer = 1136;
    public const uint WiredClickUserResponseComposer = 202;
    public const uint InClientLinkComposer = 2231;
    public const uint WiredVariableFxConfigsComposer = 9473;
    public const uint WiredVariableFxConfigsRemovedComposer = 9474;
    public const uint WiredVariableFxStatusComposer = 9475;
    public const uint WiredVariableFxStatusRemovedComposer = 9476;
    public const uint WiredUserVariablesDataComposer = 5103;
    public const uint WiredAllVariablesHashComposer = 2585;
    public const uint WiredAllVariablesDiffComposer = 2364;
    public const uint WiredVariableHoldersPageComposer = 9461;
    public const uint WiredVariableHoldersComposer = 9462;
    public const uint HideWiredConfigComposer = 1035;
    public const uint WiredEffectConfigComposer = 560;
    public const uint WiredSelectorConfigComposer = 1726;
    public const uint WiredAddonConfigComposer = 468;
    public const uint WiredVariableConfigComposer = 469;
    public const uint WiredConditionConfigComposer = 1419;
    public const uint WiredTriggeRconfigComposer = 63;
    public const uint MoodlightConfigComposer = 3944;
    public const uint GroupFurniSettingsComposer = 1315;
    public const uint OpenGiftComposer = 2950;

    // Navigator
    public const uint UpdateFavouriteRoomComposer = 2390;
    public const uint NavigatorLiftedRoomsComposer = 895;
    public const uint NavigatorPreferencesComposer = 1634;
    public const uint NavigatorFlatCatsComposer = 1047;
    public const uint NavigatorMetaDataParserComposer = 3730;
    public const uint NavigatorCollapsedCategoriesComposer = 802;

    // Messenger
    public const uint BuddyListComposer = 2205;
    public const uint FriendRequestsComposer = 3278;
    public const uint NewBuddyRequestComposer = 1345;

    // Moderation
    public const uint ModeratorInitComposer = 3507;
    public const uint ModeratorUserRoomVisitsComposer = 3384;
    public const uint ModeratorRoomChatlogComposer = 1904;
    public const uint ModeratorUserInfoComposer = 2335;
    public const uint ModeratorSupportTicketResponseComposer = 2572;
    public const uint ModeratorUserChatlogComposer = 1069;
    public const uint ModeratorRoomInfoComposer = 578;
    public const uint ModeratorSupportTicketComposer = 3110;
    public const uint ModeratorTicketChatlogComposer = 1249;
    public const uint CallForHelpPendingCallsComposer = 1608;
    public const uint CfhTopicsInitComposer = 977;

    // Inventory
    public const uint CreditBalanceComposer = 2470;
    public const uint BadgesComposer = 3884;
    public const uint FurniListAddComposer = 1600;
    public const uint FurniListNotificationComposer = 3635;
    public const uint FurniListRemoveComposer = 366;
    public const uint FurniListComposer = 956;
    public const uint FurniListUpdateComposer = 1635;
    public const uint AvatarEffectsComposer = 137;
    public const uint AvatarEffectActivatedComposer = 5;
    public const uint AvatarEffectExpiredComposer = 2643;
    public const uint AvatarEffectAddedComposer = 3835;
    public const uint TradingErrorComposer = 3558;
    public const uint TradingAcceptComposer = 316;
    public const uint TradingStartComposer = 1204;
    public const uint TradingUpdateComposer = 587;
    public const uint TradingClosedComposer = 2645;
    public const uint TradingCompleteComposer = 2537;
    public const uint TradingFinishComposer = 2890;

    // Inventory Achievements
    public const uint AchievementsComposer = 3985;
    public const uint AchievementScoreComposer = 1661;
    public const uint AchievementUnlockedComposer = 497;
    public const uint AchievementProgressedComposer = 1225;

    // Notifications
    public const uint ActivityPointsComposer = 2767;
    public const uint HabboActivityPointNotificationComposer = 1307;

    // Users
    public const uint ScrSendUserInfoComposer = 3416;
    public const uint IgnoredUsersComposer = 726;

    // Groups
    public const uint UnknownGroupComposer = 3751;
    public const uint GroupMembershipRequestedComposer = 3703;
    public const uint ManageGroupComposer = 2076;
    public const uint HabboGroupBadgesComposer = 741;
    public const uint NewGroupInfoComposer = 2276;
    public const uint GroupInfoComposer = 1266;
    public const uint GroupCreationWindowComposer = 1830;
    public const uint SetGroupIdComposer = 1890;
    public const uint GroupMembersComposer = 2544;
    public const uint UpdateFavouriteGroupComposer = 881;
    public const uint GroupMemberUpdatedComposer = 1968;
    public const uint GroupConfirmRemoveMemberComposer = 65505;
    public const uint GroupDeactivatedComposer = 2041;
    public const uint RefreshFavouriteGroupComposer = 2649;

    // Group Forums
    public const uint ForumsListDataComposer = 3026;
    public const uint ForumDataComposer = 3276;
    public const uint ThreadCreatedComposer = 1784;
    public const uint ThreadDataComposer = 1599;
    public const uint ThreadsListDataComposer = 1385;
    public const uint ThreadUpdatedComposer = 1590;
    public const uint ThreadReplyComposer = 3900;
    public const uint ForumsUnreadCountComposer = 1512;

    // Sound
    public const uint SoundSettingsComposer = 2839;

    public const uint AvatarAspectUpdateComposer = 1611;
    public const uint HelperToolComposer = 2768;
    public const uint RoomErrorNotifComposer = 630;
    public const uint FollowFriendFailedComposer = 459;

    public const uint FindFriendsProcessResultComposer = 2330;
    public const uint UserChangeComposer = 2130;
    public const uint FloorHeightMapComposer = 351;
    public const uint RoomInfoUpdatedComposer = 2147;
    public const uint MessengerErrorComposer = 1624;
    public const uint MarketplaceCanMakeOfferResultComposer = 245;
    public const uint GameAccountStatusComposer = 552;
    public const uint GuestRoomSearchResultComposer = 686;
    public const uint NewUserExperienceGiftOfferComposer = 507;
    public const uint UpdateUsernameComposer = 1298;
    public const uint VoucherRedeemOkComposer = 2458;
    public const uint FigureSetIdsComposer = 3344;
    public const uint StickyNoteComposer = 2703;
    public const uint UserRemoveComposer = 1114;
    public const uint GetGuestRoomResultComposer = 2249;
    public const uint DoorbellComposer = 2709;

    public const uint GiftWrappingConfigurationComposer = 810;
    public const uint GetRelationshipsComposer = 1535;
    public const uint FriendNotificationComposer = 861;
    public const uint BadgeEditorPartsComposer = 2661;
    public const uint TraxSongInfoComposer = 1236;
    public const uint PostUpdatedComposer = 3463;
    public const uint UserUpdateComposer = 2029;
    public const uint MutedComposer = 908;
    public const uint MarketplaceConfigurationComposer = 3502;
    public const uint CheckGnomeNameComposer = 2135;
    public const uint OpenBotActionComposer = 3409;
    public const uint FavouritesComposer = 3375;
    public const uint TalentLevelUpComposer = 1105;

    public const uint UserTagsComposer = 65514;
    public const uint CampaignComposer = 2733;
    public const uint RoomEventComposer = 898;
    public const uint MarketplaceItemStatsComposer = 1318;
    public const uint HabboSearchResultComposer = 2841;
    public const uint PetHorseFigureInformationComposer = 2048;
    public const uint PetInventoryComposer = 194;
    public const uint PingComposer = 682;
    public const uint RentableSpaceComposer = 2990;
    public const uint GetYouTubePlaylistComposer = 3180;
    public const uint RespectNotificationComposer = 614;
    public const uint RecyclerPrizesComposer = 2384;
    public const uint RecyclerStatusComposer = 3810;
    public const uint RecyclerFinishedComposer = 1680;
    public const uint GetRoomBannedUsersComposer = 811;
    public const uint RoomRatingComposer = 967;
    public const uint PlayableGamesComposer = 65482;
    public const uint TalentTrackLevelComposer = 1238;
    public const uint JoinQueueComposer = 65499;
    public const uint MarketPlaceOwnOffersComposer = 3062;
    public const uint PetBreedingComposer = 129;
    public const uint SubmitBullyReportComposer = 1969;
    public const uint UserNameChangeComposer = 3449;
    public const uint FriendFurniStartConfirmationComposer = 101;
    public const uint SendBullyReportComposer = 356;
    public const uint VoucherRedeemErrorComposer = 3077;
    public const uint PurchaseErrorComposer = 1173;
    public const uint CampaignCalendarDoorOpenedComposer = 90;
    public const uint FriendListUpdateComposer = 3955;

    public const uint UserFlatCatsComposer = 2922;
    public const uint UpdateFreezeLivesComposer = 1700;
    public const uint UnbanUserFromRoomComposer = 645;
    public const uint PetTrainingPanelComposer = 200;
    public const uint FlatAccessDeniedComposer = 3973;
    public const uint LatencyPingResponseComposer = 1707;
    public const uint HabboUserBadgesComposer = 1433;
    public const uint HeightMapUpdateComposer = 65424;
    public const uint HeightMapComposer = 742;

    public const uint CanCreateRoomComposer = 3489;
    public const uint InstantMessageErrorComposer = 3896;
    public const uint GnomeBoxComposer = 3804;
    public const uint IgnoreStatusComposer = 1828;
    public const uint PetInformationComposer = 3890;
    public const uint NavigatorSearchResultSetComposer = 77;
    public const uint ConcurrentUsersGoalProgressComposer = 3550;
    public const uint VideoOffersRewardsComposer = 1866;
    public const uint SanctionStatusComposer = 1749;
    public const uint GetYouTubeVideoComposer = 3834;
    public const uint CheckPetNameComposer = 326;
    public const uint RespectPetNotificationComposer = 1504;
    public const uint EnforceCategoryUpdateComposer = 1665;
    public const uint CommunityGoalHallOfFameComposer = 3602;
    public const uint RoomOccupiedTilesComposer = 1914;
    public const uint SendGameInvitationComposer = 65415;
    public const uint GiftWrappingErrorComposer = 3258;
    public const uint PromoArticlesComposer = 3226;
    public const uint Game1WeeklyLeaderboardComposer = 65423;
    public const uint RentableSpacesErrorComposer = 500;
    public const uint AddExperiencePointsComposer = 1177;
    public const uint GetRoomFilterListComposer = 1770;
    public const uint GameAchievementListComposer = 2725;
    public const uint PromotableRoomsComposer = 2713;
    public const uint RoomEntryTileComposer = 591;
    public const uint RoomEntryInfoComposer = 1963;
    public const uint RoomNotificationComposer = 2778;
    public const uint ClubGiftsComposer = 3374;
    public const uint MOTDNotificationComposer = 1491;
    public const uint PopularRoomTagsResultComposer = 27;
    public const uint NewConsoleMessageComposer = 1299;
    public const uint RoomPropertyComposer = 780;
    public const uint MarketPlaceOffersComposer = 821;
    public const uint TalentTrackComposer = 3185;
    public const uint ProfileInformationComposer = 295;
    public const uint BadgeDefinitionsComposer = 1843;
    public const uint Game2WeeklyLeaderboardComposer = 1342;
    public const uint NameChangeUpdateComposer = 1852;
    public const uint RoomVisualizationSettingsComposer = 2632;
    public const uint MarketplaceMakeOfferResultComposer = 3162;
    public const uint FlatCreatedComposer = 1121;
    public const uint BotInventoryComposer = 2723;
    public const uint LoadGameComposer = 65477;
    public const uint UpdateMagicTileComposer = 65486;
    public const uint MaintenanceStatusComposer = 864;
    public const uint Game3WeeklyLeaderboardComposer = 1397;
    public const uint GameListComposer = 65500;
    public const uint RoomMuteSettingsComposer = 2905;
    public const uint RoomInviteComposer = 3574;
    public const uint FriendFurniOtherLockConfirmedComposer = 2266;
    public const uint FriendFurniCancelLockComposer = 65420;
    public const uint BroadcastMessageAlertComposer = 3380;
    public const uint MarketplaceCancelOfferResultComposer = 3615;
    public const uint MarketplaceBuyOfferResultComposer = 144;
    public const uint NavigatorSettingsComposer = 531;

    public const uint MessengerInitComposer = 1748;
    public const uint PollContentsComposer = 1338;
    public const uint PollOfferComposer = 2521;
    public const uint RoomUseHabbiconComposer = 1604;
    public const uint AllowedChatStylesComposer = 9340;
    public const uint CreatableRoomModelsComposer = 9341;
    public const uint UserHabbiconsComposer = 1858;
    public const uint UserHabbiconStatusChangedComposer = 1723;
    public const uint HabbiconShopDataComposer = 3739;
    public const uint HabbiconInfoComposer = 1391;
    public const uint MessengerMessageAckComposer = 4902;
    public const uint MessengerMessageFailedComposer = 4903;
    public const uint MessengerMessageComposer = 4904;
    // Camera internal IDs; wire IDs are revision-specific.
    public const uint InitCameraComposer = 20;
    public const uint CameraStorageUrlComposer = 2699;
    public const uint CameraPurchaseOKComposer = 1100;
    public const uint CameraPublishStatusComposer = 2499;
    public const uint CompetitionStatusComposer = 3010;
    public const uint ThumbnailStatusComposer = 2964;
    public const uint RewardTracksComposer = 2475;
    public const uint RewardTrackClaimResultComposer = 1438;
    public const uint RewardTrackProgressComposer = 3817;
    public const uint RewardTrackPremiumPurchaseResultComposer = 1829;
    public const uint HabboClubExtendOfferComposer = 3860;
    public const uint ClubGiftReceivedComposer = 3965;
    public const uint PickMonthlyClubGiftComposer = 743;
    // SnowStorm (AIR Game2 payloads); internal IDs match Volt's wire IDs except where noted.
    public const uint Game2GameDirectoryStatusComposer = 1649;
    public const uint Game2GameCreatedComposer = 5000;
    public const uint Game2InArenaQueueComposer = 567;
    public const uint Game2GameLongDataComposer = 5002;
    public const uint Game2StartCounterComposer = 5003;
    public const uint Game2UserJoinedGameComposer = 5004;
    public const uint Game2UserLeftGameComposer = 136;
    public const uint Game2StopCounterComposer = 1862;
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
    public const uint Game2JoiningGameFailedComposer = 2370;
    public const uint Game2StartingGameFailedComposer = 3207;
    public const uint Game2GameCancelledComposer = 588;
    public const uint Game2GameNotFoundComposer = 65467;
    public const uint Game2UserBlockedComposer = 2002;
    public const uint Game2TotalLeaderboardComposer = 273;
    public const uint Game2FriendsLeaderboardComposer = 2071;
    public const uint Game2WeeklyFriendsLeaderboardComposer = 2774;
    public const uint Game2TotalGroupLeaderboardComposer = 3434;
    public const uint Game2WeeklyGroupLeaderboardComposer = 3992;
    public const uint SnowWarGameTokensComposer = 2966;
    public const uint CraftableProductsComposer = 1472;
    public const uint CraftingRecipeComposer = 730;
    public const uint CraftingResultComposer = 3282;
    public const uint CraftingRecipesAvailableComposer = 2942;
    // Room music
    public const uint JukeboxPlaylistComposer = 1348;
    public const uint JukeboxPlaylistFullComposer = 3403;
    public const uint NowPlayingComposer = 3910;
    public const uint OfficialSongIdComposer = 3757;
    public const uint SoundMachinePlaylistComposer = 2973;
    public const uint SongDisksInventoryComposer = 3277;
    public const uint QuizDataComposer = 3830;
    public const uint QuizResultsComposer = 3020;
    public const uint CampaignCalendarDataComposer = 395;
    public const uint WiredVariableInspectionDataComposer = 9483;
    public const uint WiredEnvironmentComposer = 3583;
    public const uint AreaHideComposer = 496;
    public const uint ConfigurationItemStatesComposer = 3660;
    public const uint WiredUserVariablesData64Composer = 9480;
    public const uint WiredVariableHolders64Composer = 9481;
    public const uint WiredVariableHoldersPage64Composer = 9482;
    public const uint WiredChestContentsComposer = 9312;
    public const uint WiredChestFurniChunkComposer = 9322;
    public const uint WiredChestTradeOpenComposer = 9331;
    public const uint WiredChestTradeItemsComposer = 9332;
    public const uint WiredChestTradeCancelledComposer = 9333;
    public const uint WiredChestTradeCompletedComposer = 9334;
    public const uint WiredChestRewardComposer = 9346;
    public const uint WiredChestUpgradeComposer = 9335;
    public const uint WiredChestSettingsAckComposer = 9347;
    public const uint WiredChestLockComposer = 9329;

    // October wired boxes (r88). Ids are the WIN63-202610121709 equivalents of the WIN63-202610131318-860371703 classes named.
    public const uint WiredFurniTransparencyComposer = 3710; // class_3390 (new 2321)
    public const uint WiredUserTransparencyComposer = 536; // class_2828 (new 3664)
    public const uint WiredFurniHighlightComposer = 2218; // class_2680 (new 2380)
    public const uint WiredWebApiKeyComposer = 467; // class_3530 (new 162)
    public const uint WiredVariableLeaderboardSnapshotComposer = 1284; // package_160.class_3569 (new 1494)
    public const uint WiredVariableLeaderboardChunkComposer = 995; // package_160.class_3168 (new 3979)
    public const uint WiredLabelsComposer = 656; // class_3043 (new 2237)
    public const uint WiredLabelsUpdateComposer = 1240; // class_2856 (new 796)
    public const uint WiredLinkOpenComposer = 2161; // package_82.class_3692 (new 1715)
    public const uint WiredLinkConfigurationComposer = 778; // package_82.class_3192 (new 3405)
    public const uint WiredLinkOperationResultComposer = 1586; // package_82.class_3722 (new 3961)
    public const uint WiredLinkConnectionsComposer = 830; // package_82.class_2742 (new 3962)
    public const uint WiredLinkNetworkInvalidatedComposer = 1949; // package_82.class_2356 (new 999)
    public const uint WiredUserHighlightComposer = 2552; // class_3603 (new 1684)
    public const uint WiredFurniVisualizationStateComposer = 3355; // class_3560 (new 1826)
    public const uint WiredUserVisualizationStateComposer = 2901; // class_3459 (new 1454)
    public const uint ObjectRemoveConfirmComposer = 3498; // package_64/class_3455 (new 1631)
    public const uint YouArePlayingGameComposer = 1293; // package_44/class_3256 (new 403)
}
