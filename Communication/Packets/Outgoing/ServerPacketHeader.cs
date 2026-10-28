namespace Plus.Communication.Packets.Outgoing;

public static class ServerPacketHeader
{
    // Handshake
    public const uint SimplePollStartComposer = 2538;
    public const uint SimplePollAnswerComposer = 2319;
    public const uint SimplePollAnswersComposer = 726;
    public const uint InitDiffieHandshakeComposer = 2957;
    public const uint SecretKeyComposer = 729;
    public const uint AuthenticationOkComposer = 3736;
    public const uint UserObjectComposer = 1284;
    public const uint UserPerksComposer = 1963;
    public const uint KickbackInfoComposer = 3618;
    public const uint UserRightsComposer = 3668;
    public const uint GenericErrorComposer = 3;
    public const uint DisconnectReasonComposer = 4000;
    public const uint SetUniqueIdComposer = 65511;
    public const uint AvailabilityStatusComposer = 1182;

    // Avatar
    public const uint WardrobeComposer = 1522;
    public const uint UserNftWardrobeComposer = 2182;
    public const uint SelectedNftWardrobeOutfitComposer = 3377;

    // Collectibles hub
    public const uint CollectibleWalletAddressesComposer = 3185;
    public const uint CollectorScoreComposer = 2659;
    public const uint NftCollectionsComposer = 667;
    public const uint NftCollectionBonusClaimResultComposer = 154;
    public const uint NftCollectionRewardClaimResultComposer = 2730;
    public const uint NftStoreOffersComposer = 3151;
    public const uint NftStorePurchaseResultComposer = 2908;
    public const uint CollectibleMintableItemTypesComposer = 131;
    public const uint CollectibleMintingEnabledComposer = 2735;
    public const uint MintCollectibleResultComposer = 595;
    public const uint NftClaimsComposer = 2824;
    public const uint NftClaimResultComposer = 1957;
    public const uint EmeraldBalanceComposer = 1071;

    // Catalog
    public const uint CatalogIndexComposer = 1288;
    public const uint CatalogItemDiscountComposer = 2779;
    public const uint BuildersClubSubscriptionStatusComposer = 2585;
    public const uint BuildersClubFurniCountComposer = 720;
    public const uint BuildersClubPlacementWarningComposer = 1148;
    public const uint PurchaseOKComposer = 439;
    public const uint CatalogOfferComposer = 1067;
    public const uint CatalogPageComposer = 140;
    public const uint CatalogUpdatedComposer = 3805;
    public const uint HabboClubOffersComposer = 1693;
    public const uint SellablePetBreedsComposer = 2049;
    public const uint GroupFurniConfigComposer = 213;
    public const uint PresentDeliverErrorComposer = 1256;

    // Quests
    public const uint QuestListComposer = 3351;
    public const uint QuestCompletedComposer = 1020;
    public const uint QuestAbortedComposer = 3812;
    public const uint QuestStartedComposer = 225;

    // Room Avatar
    public const uint ActionComposer = 3887;
    public const uint SleepComposer = 2281;
    public const uint DanceComposer = 3612;
    public const uint CarryObjectComposer = 3431;
    public const uint AvatarEffectComposer = 2591;

    // Room Chat
    public const uint ChatComposer = 1389;
    public const uint ShoutComposer = 1314;
    public const uint WhisperComposer = 3712;
    public const uint FloodControlComposer = 2665;
    public const uint UserTypingComposer = 530;

    // Room Engine
    public const uint UsersComposer = 2352;
    public const uint FurnitureAliasesComposer = 11;
    public const uint ObjectAddComposer = 883;
    public const uint ObjectsComposer = 3992;
    public const uint ObjectUpdateComposer = 594;
    public const uint ObjectDataUpdateComposer = 1162;
    public const uint ObjectsDataUpdateComposer = 1272;
    public const uint FurniRecolorUpdateComposer = 2911;
    public const uint ObjectRemoveComposer = 1980;
    public const uint ObjectRemoveMultipleComposer = 3563;
    public const uint SlideObjectBundleComposer = 2534;
    public const uint ItemsComposer = 2808;
    public const uint ItemAddComposer = 2732;
    public const uint ItemUpdateComposer = 3740;
    // Plus-only id, not an official one: furni metadata for the entries above. 5120 is free in the client's and Volt-Renderer's
    // incoming tables.
    public const uint FurnitureMetadataComposer = 5120;
    public const uint ItemRemoveComposer = 398;

    // Room Session
    public const uint RoomForwardComposer = 2686;
    public const uint RoomReadyComposer = 3670;
    public const uint OpenConnectionComposer = 665;
    public const uint CloseConnectionComposer = 1391;
    public const uint FlatAccessibleComposer = 1462;
    public const uint CantConnectComposer = 101;

    // Room Permissions
    public const uint YouAreControllerComposer = 404;
    public const uint YouAreNotControllerComposer = 2695;
    public const uint YouAreOwnerComposer = 523;

    // Room Settings
    public const uint RoomSettingsDataComposer = 2303;
    public const uint RoomSettingsSavedComposer = 2446;
    public const uint FlatControllerRemovedComposer = 2350;
    public const uint FlatControllerAddedComposer = 911;
    public const uint RoomRightsListComposer = 3111;

    // Room Furniture
    public const uint WiredValidationErrorComposer = 2682;
    public const uint WiredMonitorDataComposer = 5101;
    public const uint WiredRoomLogPageComposer = 2060;
    public const uint WiredRoomSettingsDataComposer = 5102;
    public const uint WiredRoomSettingsComposer = 3767;
    public const uint WiredRewardResultComposer = 2763;
    public const uint WiredMovementsComposer = 965;
    public const uint WiredFurniMoveStyleComposer = 5110;
    public const uint WiredClickSettingsComposer = 1456;
    public const uint WiredClickUserResponseComposer = 3690;
    public const uint InClientLinkComposer = 3162;
    public const uint WiredVariableFxConfigsComposer = 1419;
    public const uint WiredVariableFxConfigsRemovedComposer = 1513;
    public const uint WiredVariableFxStatusComposer = 3194;
    public const uint WiredVariableFxStatusRemovedComposer = 1908;
    public const uint WiredUserVariablesDataComposer = 5103;
    public const uint WiredAllVariablesHashComposer = 3653;
    public const uint WiredAllVariablesDiffComposer = 2896;
    public const uint WiredVariableHoldersPageComposer = 9461;
    public const uint WiredVariableHoldersComposer = 9462;
    public const uint HideWiredConfigComposer = 2025;
    public const uint WiredEffectConfigComposer = 948;
    public const uint WiredSelectorConfigComposer = 3823;
    public const uint WiredAddonConfigComposer = 3070;
    public const uint WiredVariableConfigComposer = 2430;
    public const uint WiredConditionConfigComposer = 777;
    public const uint WiredTriggeRconfigComposer = 2318;
    public const uint MoodlightConfigComposer = 1466;
    public const uint GroupFurniSettingsComposer = 3980;
    public const uint OpenGiftComposer = 1550;

    // Navigator
    public const uint UpdateFavouriteRoomComposer = 3766;
    public const uint NavigatorLiftedRoomsComposer = 928;
    public const uint NavigatorPreferencesComposer = 438;
    public const uint NavigatorFlatCatsComposer = 1977;
    public const uint NavigatorMetaDataParserComposer = 3664;
    public const uint NavigatorCollapsedCategoriesComposer = 1875;

    // Messenger
    public const uint BuddyListComposer = 1392;
    public const uint FriendRequestsComposer = 1499;
    public const uint NewBuddyRequestComposer = 2667;

    // Moderation
    public const uint ModeratorInitComposer = 3833;
    public const uint ModeratorUserRoomVisitsComposer = 1668;
    public const uint ModeratorRoomChatlogComposer = 1523;
    public const uint ModeratorUserInfoComposer = 137;
    public const uint ModeratorSupportTicketResponseComposer = 3404;
    public const uint ModeratorUserChatlogComposer = 1784;
    public const uint ModeratorRoomInfoComposer = 781;
    public const uint ModeratorSupportTicketComposer = 3150;
    public const uint ModeratorTicketChatlogComposer = 1571;
    public const uint CallForHelpPendingCallsComposer = 1491;
    public const uint CfhTopicsInitComposer = 958;

    // Inventory
    public const uint CreditBalanceComposer = 198;
    public const uint BadgesComposer = 2946;
    public const uint FurniListAddComposer = 2713;
    public const uint FurniListNotificationComposer = 2193;
    public const uint FurniListRemoveComposer = 3133;
    public const uint FurniListComposer = 1413;
    public const uint FurniListUpdateComposer = 754;
    public const uint AvatarEffectsComposer = 874;
    public const uint AvatarEffectActivatedComposer = 2044;
    public const uint AvatarEffectExpiredComposer = 3795;
    public const uint AvatarEffectAddedComposer = 35;
    public const uint TradingErrorComposer = 3630;
    public const uint TradingAcceptComposer = 2840;
    public const uint TradingStartComposer = 2002;
    public const uint TradingUpdateComposer = 1897;
    public const uint TradingClosedComposer = 2100;
    public const uint TradingCompleteComposer = 172;
    public const uint TradingFinishComposer = 2954;

    // Inventory Achievements
    public const uint AchievementsComposer = 1057;
    public const uint AchievementScoreComposer = 3316;
    public const uint AchievementUnlockedComposer = 3114;
    public const uint AchievementProgressedComposer = 1871;

    // Notifications
    public const uint ActivityPointsComposer = 813;
    public const uint HabboActivityPointNotificationComposer = 1490;

    // Users
    public const uint ScrSendUserInfoComposer = 3792;
    public const uint IgnoredUsersComposer = 3311;

    // Groups
    public const uint UnknownGroupComposer = 3687;
    public const uint GroupMembershipRequestedComposer = 1202;
    public const uint ManageGroupComposer = 344;
    public const uint HabboGroupBadgesComposer = 3343;
    public const uint NewGroupInfoComposer = 1426;
    public const uint GroupInfoComposer = 865;
    public const uint GroupCreationWindowComposer = 2699;
    public const uint SetGroupIdComposer = 2253;
    public const uint GroupMembersComposer = 1734;
    public const uint UpdateFavouriteGroupComposer = 3864;
    public const uint GroupMemberUpdatedComposer = 3936;
    public const uint GroupConfirmRemoveMemberComposer = 65505;
    public const uint GroupDeactivatedComposer = 3947;
    public const uint RefreshFavouriteGroupComposer = 2123;

    // Group Forums
    public const uint ForumsListDataComposer = 1269;
    public const uint ForumDataComposer = 2924;
    public const uint ThreadCreatedComposer = 1794;
    public const uint ThreadDataComposer = 3240;
    public const uint ThreadsListDataComposer = 642;
    public const uint ThreadUpdatedComposer = 2563;
    public const uint ThreadReplyComposer = 2304;
    public const uint ForumsUnreadCountComposer = 1620;

    // Sound
    public const uint SoundSettingsComposer = 692;

    public const uint AvatarAspectUpdateComposer = 538;
    public const uint HelperToolComposer = 1555;
    public const uint RoomErrorNotifComposer = 2881;
    public const uint FollowFriendFailedComposer = 1592;

    public const uint FindFriendsProcessResultComposer = 1418;
    public const uint UserChangeComposer = 920;
    public const uint FloorHeightMapComposer = 2935;
    public const uint RoomInfoUpdatedComposer = 3067;
    public const uint MessengerErrorComposer = 242;
    public const uint MarketplaceCanMakeOfferResultComposer = 170;
    public const uint GameAccountStatusComposer = 1569;
    public const uint GuestRoomSearchResultComposer = 332;
    public const uint NewUserExperienceGiftOfferComposer = 899;
    public const uint UpdateUsernameComposer = 1367;
    public const uint VoucherRedeemOkComposer = 399;
    public const uint FigureSetIdsComposer = 2153;
    public const uint StickyNoteComposer = 2024;
    public const uint UserRemoveComposer = 1792;
    public const uint GetGuestRoomResultComposer = 3303;
    public const uint DoorbellComposer = 3768;

    public const uint GiftWrappingConfigurationComposer = 3531;
    public const uint GetRelationshipsComposer = 2874;
    public const uint FriendNotificationComposer = 462;
    public const uint BadgeEditorPartsComposer = 409;
    public const uint TraxSongInfoComposer = 2743;
    public const uint PostUpdatedComposer = 3094;
    public const uint UserUpdateComposer = 3744;
    public const uint MutedComposer = 3559;
    public const uint MarketplaceConfigurationComposer = 1543;
    public const uint CheckGnomeNameComposer = 92;
    public const uint OpenBotActionComposer = 14;
    public const uint FavouritesComposer = 1156;
    public const uint TalentLevelUpComposer = 3796;

    public const uint UserTagsComposer = 65514;
    public const uint CampaignComposer = 1085;
    public const uint RoomEventComposer = 3867;
    public const uint MarketplaceItemStatsComposer = 2765;
    public const uint HabboSearchResultComposer = 780;
    public const uint PetHorseFigureInformationComposer = 3885;
    public const uint PetInventoryComposer = 3912;
    public const uint PingComposer = 2687;
    public const uint RentableSpaceComposer = 2076;
    public const uint GetYouTubePlaylistComposer = 1567;
    public const uint RespectNotificationComposer = 2431;
    public const uint RecyclerPrizesComposer = 306;
    public const uint RecyclerStatusComposer = 61;
    public const uint RecyclerFinishedComposer = 3943;
    public const uint GetRoomBannedUsersComposer = 2923;
    public const uint RoomRatingComposer = 2013;
    public const uint PlayableGamesComposer = 65482;
    public const uint TalentTrackLevelComposer = 2975;
    public const uint JoinQueueComposer = 65499;
    public const uint MarketPlaceOwnOffersComposer = 2309;
    public const uint PetBreedingComposer = 1920;
    public const uint SubmitBullyReportComposer = 1641;
    public const uint UserNameChangeComposer = 520;
    public const uint FriendFurniStartConfirmationComposer = 139;
    public const uint GuideReportingStatusComposer = 3244;
    public const uint VoucherRedeemErrorComposer = 1252;
    public const uint PurchaseErrorComposer = 602;
    public const uint CampaignCalendarDoorOpenedComposer = 3267;
    public const uint FriendListUpdateComposer = 3652;

    public const uint UserFlatCatsComposer = 558;
    public const uint UpdateFreezeLivesComposer = 573;
    public const uint UnbanUserFromRoomComposer = 800;
    public const uint PetTrainingPanelComposer = 1149;
    public const uint FlatAccessDeniedComposer = 1610;
    public const uint LatencyPingResponseComposer = 2191;
    public const uint HabboUserBadgesComposer = 964;
    public const uint HeightMapUpdateComposer = 2234;
    public const uint HeightMapComposer = 3681;

    public const uint CanCreateRoomComposer = 3158;
    public const uint InstantMessageErrorComposer = 3301;
    public const uint GnomeBoxComposer = 3913;
    public const uint IgnoreStatusComposer = 871;
    public const uint PetInformationComposer = 2669;
    public const uint NavigatorSearchResultSetComposer = 203;
    public const uint ConcurrentUsersGoalProgressComposer = 2293;
    public const uint VideoOffersRewardsComposer = 3544;
    public const uint SanctionStatusComposer = 493;
    public const uint GetYouTubeVideoComposer = 2842;
    public const uint CheckPetNameComposer = 870;
    public const uint RespectPetNotificationComposer = 3793;
    public const uint EnforceCategoryUpdateComposer = 673;
    public const uint CommunityGoalHallOfFameComposer = 2240;
    public const uint RoomOccupiedTilesComposer = 480;
    public const uint SendGameInvitationComposer = 65415;
    public const uint GiftWrappingErrorComposer = 369;
    public const uint PromoArticlesComposer = 1454;
    public const uint Game1WeeklyLeaderboardComposer = 65423;
    public const uint RentableSpacesErrorComposer = 1917;
    public const uint AddExperiencePointsComposer = 3213;
    public const uint GetRoomFilterListComposer = 3592;
    public const uint GameAchievementListComposer = 1815;
    public const uint PromotableRoomsComposer = 1584;
    public const uint RoomEntryTileComposer = 2714;
    public const uint RoomEntryInfoComposer = 1950;
    public const uint RoomNotificationComposer = 3295;
    public const uint ClubGiftsComposer = 3697;
    public const uint MOTDNotificationComposer = 3856;
    public const uint PopularRoomTagsResultComposer = 95;
    public const uint NewConsoleMessageComposer = 2993;
    public const uint RoomPropertyComposer = 2085;
    public const uint MarketPlaceOffersComposer = 3635;
    public const uint TalentTrackComposer = 3555;
    public const uint ProfileInformationComposer = 132;
    public const uint BadgeDefinitionsComposer = 432;
    public const uint Game2WeeklyLeaderboardComposer = 3413;
    public const uint NameChangeUpdateComposer = 1201;
    public const uint RoomVisualizationSettingsComposer = 3237;
    public const uint MarketplaceMakeOfferResultComposer = 354;
    public const uint FlatCreatedComposer = 3163;
    public const uint BotInventoryComposer = 2574;
    public const uint LoadGameComposer = 65477;
    public const uint UpdateMagicTileComposer = 65486;
    public const uint MaintenanceStatusComposer = 2747;
    public const uint Game3WeeklyLeaderboardComposer = 528;
    public const uint GameListComposer = 65500;
    public const uint RoomMuteSettingsComposer = 2970;
    public const uint RoomInviteComposer = 1033;
    public const uint FriendFurniOtherLockConfirmedComposer = 1048;
    public const uint FriendFurniCancelLockComposer = 65420;
    public const uint BroadcastMessageAlertComposer = 3870;
    public const uint ModeratorMessageComposer = 898;
    public const uint MarketplaceCancelOfferResultComposer = 2506;
    public const uint MarketplaceBuyOfferResultComposer = 311;
    public const uint NavigatorSettingsComposer = 3629;

    public const uint MessengerInitComposer = 2086;
    public const uint PollContentsComposer = 2851;
    public const uint PollOfferComposer = 3682;
    public const uint RoomUseHabbiconComposer = 3144;
    public const uint AllowedChatStylesComposer = 9340;
    public const uint CreatableRoomModelsComposer = 9341;
    public const uint UserHabbiconsComposer = 2087;
    public const uint UserHabbiconStatusChangedComposer = 184;
    public const uint HabbiconShopDataComposer = 2122;
    public const uint HabbiconInfoComposer = 2565;
    public const uint MessengerMessageAckComposer = 4902;
    public const uint MessengerMessageFailedComposer = 4903;
    public const uint MessengerMessageComposer = 4904;
    // Camera internal IDs; wire IDs are revision-specific.
    public const uint InitCameraComposer = 644;
    public const uint CameraStorageUrlComposer = 1751;
    public const uint CameraPurchaseOKComposer = 2413;
    public const uint CameraPublishStatusComposer = 2749;
    public const uint CompetitionStatusComposer = 950;
    public const uint ThumbnailStatusComposer = 3302;
    public const uint RewardTracksComposer = 1467;
    public const uint RewardTrackClaimResultComposer = 1119;
    public const uint RewardTrackProgressComposer = 2402;
    public const uint RewardTrackPremiumPurchaseResultComposer = 382;
    // Daily tasks.
    public const uint ActiveDailyTasksComposer = 937;
    public const uint DailyTasksAddedComposer = 1312;
    public const uint DailyTaskUpdatedComposer = 1663;
    public const uint HabboClubExtendOfferComposer = 2797;
    public const uint ClubGiftReceivedComposer = 2710;
    public const uint PickMonthlyClubGiftComposer = 3459;
    // SnowStorm (AIR Game2).
    public const uint Game2GameDirectoryStatusComposer = 3174;
    public const uint Game2GameCreatedComposer = 3460;
    public const uint Game2InArenaQueueComposer = 442;
    public const uint Game2GameLongDataComposer = 2389;
    public const uint Game2StartCounterComposer = 391;
    public const uint Game2UserJoinedGameComposer = 3794;
    public const uint Game2UserLeftGameComposer = 352;
    public const uint Game2StopCounterComposer = 2807;
    public const uint Game2GameStartedComposer = 1446;
    public const uint Game2EnterArenaComposer = 1974;
    public const uint Game2ArenaEnteredComposer = 2012;
    public const uint Game2EnterArenaFailedComposer = 1498;
    public const uint Game2GameStatusComposer = 2432;
    public const uint Game2FullGameStatusComposer = 1857;
    public const uint Game2StageStartingComposer = 1588;
    public const uint Game2StageLoadComposer = 3625;
    public const uint Game2RejoinPreviousRoomComposer = 3120;
    public const uint Game2StageStillLoadingComposer = 3562;
    public const uint Game2GameEndingComposer = 1736;
    public const uint Game2GameChatComposer = 2170;
    public const uint Game2StageRunningComposer = 2202;
    public const uint Game2StageEndingComposer = 2299;
    public const uint Game2PlayerExitedGameArenaComposer = 2270;
    public const uint Game2PlayerRematchesComposer = 1022;
    public const uint Game2JoiningGameFailedComposer = 3549;
    public const uint Game2StartingGameFailedComposer = 1545;
    public const uint Game2GameCancelledComposer = 1184;
    public const uint Game2GameNotFoundComposer = 1585;
    public const uint Game2UserBlockedComposer = 1527;
    public const uint Game2TotalLeaderboardComposer = 1210;
    public const uint Game2FriendsLeaderboardComposer = 1955;
    public const uint Game2WeeklyFriendsLeaderboardComposer = 1982;
    public const uint Game2TotalGroupLeaderboardComposer = 1638;
    public const uint Game2WeeklyGroupLeaderboardComposer = 1311;
    public const uint SnowWarGameTokensComposer = 2627;
    public const uint CraftableProductsComposer = 3843;
    public const uint CraftingRecipeComposer = 193;
    public const uint CraftingResultComposer = 857;
    public const uint CraftingRecipesAvailableComposer = 2737;
    // Room music
    public const uint JukeboxPlaylistComposer = 3256;
    public const uint JukeboxPlaylistFullComposer = 379;
    public const uint NowPlayingComposer = 2131;
    public const uint OfficialSongIdComposer = 163;
    public const uint SoundMachinePlaylistComposer = 1077;
    public const uint SongDisksInventoryComposer = 1372;
    public const uint QuizDataComposer = 1803;
    public const uint QuizResultsComposer = 114;
    public const uint CampaignCalendarDataComposer = 3939;
    public const uint WiredVariableInspectionDataComposer = 9483;
    public const uint WiredEnvironmentComposer = 2929;
    public const uint AreaHideComposer = 552;
    public const uint ConfigurationItemStatesComposer = 205;
    public const uint UserNftChatStylesComposer = 422;
    public const uint RaidProtectionCapabilityComposer = 2033;
    public const uint WiredUserVariablesData64Composer = 9480;
    public const uint WiredVariableHolders64Composer = 9481;
    public const uint WiredVariableHoldersPage64Composer = 9482;
    // Wired chests and wired transactions; see reviews/chest-protocol-map.md.
    public const uint WiredChestOpenComposer = 3315;
    public const uint WiredChestSettingsUpdatedComposer = 1453;
    public const uint WiredChestUpgradeComposer = 2439;
    public const uint WiredChestCoinsComposer = 1016;
    public const uint WiredChestFurniChunkComposer = 3110;
    public const uint WiredChestFurniUpdateComposer = 3829;
    public const uint WiredTransactionFailComposer = 180;
    public const uint WiredTransactionSuccessComposer = 270;
    public const uint WiredTradeErrorComposer = 2537;
    public const uint WiredChestTradeOpenComposer = 932;
    public const uint WiredChestTradeItemsComposer = 790;
    public const uint WiredChestTradeCancelledComposer = 3357;
    public const uint WiredChestTradeCompletedComposer = 3813;
    public const uint WiredTransactionLogListComposer = 3312;

    // October wired boxes.
    public const uint WiredFurniTransparencyComposer = 3737;
    public const uint WiredUserTransparencyComposer = 3073;
    public const uint WiredFurniHighlightComposer = 632;
    public const uint WiredWebApiKeyComposer = 2663;
    public const uint WiredVariableLeaderboardSnapshotComposer = 896;
    public const uint WiredVariableLeaderboardChunkComposer = 3783;
    public const uint WiredLabelsComposer = 2397;
    public const uint WiredLabelsUpdateComposer = 936;
    public const uint WiredLinkOpenComposer = 714;
    public const uint WiredLinkConfigurationComposer = 3788;
    public const uint WiredLinkOperationResultComposer = 1546;
    public const uint WiredLinkConnectionsComposer = 597;
    public const uint WiredLinkNetworkInvalidatedComposer = 2990;
    public const uint WiredUserHighlightComposer = 1899;
    public const uint WiredFurniVisualizationStateComposer = 1477;
    public const uint WiredUserVisualizationStateComposer = 3360;
    public const uint ObjectRemoveConfirmComposer = 3614;
    public const uint YouArePlayingGameComposer = 2472;
    public const uint WiredContractOpenComposer = 1644;
    public const uint WiredContractContentsComposer = 2882;
    public const uint WiredContractUpdateResultComposer = 2931;

    // Wired monitor.
    public const uint WiredRoomStatsComposer = 1014;
    public const uint WiredErrorLogsComposer = 3349;
    // Wired menu permissions, sent on room entry.
    public const uint WiredPermissionsComposer = 635;
    // Answers WiredGetVariablesForObject.
    public const uint WiredVariablesForObjectComposer = 876;
    // Answers of the Variables tab's Highlight, Delete and Manage buttons.
    public const uint WiredAllVariableHoldersComposer = 1596;
    public const uint WiredUserVariablesListComposer = 3704;
    public const uint WiredUserPermanentVariablesComposer = 3853;
    public const uint WiredSetUserPermanentVariableResultComposer = 3359;
    public const uint WiredMenuErrorComposer = 770;

    // Free product claims (cursed dragon adoption) and the new-feature promo expiry.
    public const uint HasClaimedProductResponseComposer = 3508;
    public const uint ClaimProductResultComposer = 897;
    public const uint SecondsUntilComposer = 888;
}
