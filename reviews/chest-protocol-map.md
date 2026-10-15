# Wired chest / transaction / contract protocol map

Scope: every message the official AIR client uses for Wired chests, Wired transactions (wired trades) and
Wired contracts, mapped to the ids PlusEMU and Volt-Renderer must use.

## Builds and evidence

| Role | Build | Message map |
|---|---|---|
| Current AIR (live hhus) | `WIN63-202610131318-860371703` | `decompiled/scripts/com/sulake/habbo/communication/class_2049.as` (habbo-intelligence index) |
| Id space of PlusEMU + Volt-Renderer | `WIN63-202610121709-785561319` | `/home/krypton/habbo-wired-runtime-20261012/protocol/current-source-sol-r1/decompiled/scripts/com/sulake/habbo/communication/class_2049.as` |

The 121709 build was indexed into habbo-intelligence for this work
(`index/WIN63-202610121709-785561319`, map sha256 `1318ac79...efaf97`), so every old id below comes from
`air_message(build=WIN63-202610121709-785561319)` / the index, not from a guess.

Identity evidence: both builds keep the same obfuscated class names (9039 classes each). For every row the
message class, its parser / composer class and every data class it reads (`ChestStorage`, `ChestItemType`,
`TradeRequirement*`, `TradeRequirementNode`, `class_3481`, `class_2591`, `WiredTransactionSuccessContents`)
are byte-identical between the two dumps once `var_NNN` / `const_NNN` renames are normalised. So the class
name plus the identical constructor/parse body is the identity proof; the header number is the only thing
that changed.

AIR source roots (131318): `com/sulake/habbo/roomevents/wired_trading/**` (chests, contracts, transactions),
`com/sulake/habbo/inventory/wired_trading/**` (trade window), `com/sulake/habbo/inventory/class_1962.as:1183-1256`
(trade handlers), `com/sulake/habbo/notifications/class_1962.as:685-715` (transaction notifications).

## Client -> server

| AIR 131318 id | Class (131318 path) | Fields (AIR order) | Old 121709 id (map line) | PlusEMU before | Volt-Renderer before | Gap / fix |
|---|---|---|---|---|---|---|
| 1457 | `package_95/class_2544` open chest. Sent by `WiredChestController.open` (`WiredChestController.as:135`) | int chestId | **1595** (L712) | `ChestOpenEvent` 9327 | `ChestOpenComposer` 9327, also sent straight from `RoomObjectEventHandler` on furni use | invented id. AIR only sends it after server `ChestOpen` (3966) |
| 3568 | `package_95/class_2979` close chest (`WiredChestController.as:151`) | int chestId | **1154** (L641) | `ChestCloseEvent` 9339 | `ChestCloseComposer` 9339 | invented id |
| 3947 | `package_95/class_3668` start deposit (`WiredChestWrapperView.as:232`) | int chestId | **3268** (L983) | `ChestStartDepositEvent` 9324 | `ChestStartDepositComposer` 9324 | invented id. Deposit then runs through the wired trade window |
| 2074 | `package_95/class_3881` withdraw all (`WiredChestWrapperView.as:245`) | int chestId | **57** (L1146) | `ChestWithdrawAllEvent` 9326 (furni only) | `ChestWithdrawAllFurniComposer` 9326; coin chests sent `ChestWithdraw(id,-1,-1)` | invented id. AIR uses it for coin chests too |
| 3992 | `package_95/class_2388` withdraw furni type (`FurniChestSubController.as:101`) | int chestId, ChestItemType(bool wall, int typeId, string posterId), int amount | **715** (L1172) | `ChestWithdrawFurniEvent` 9320 (same shape) | `ChestWithdrawFurniComposer` 9320 | invented id only |
| 1840 | `package_95/class_3305` withdraw coins (`CoinChestSubController.as:62`) | int chestId, int amount | **3731** (L1059) | `ChestWithdrawCoinsEvent` 9314: chestId, **currency**, amount | `ChestWithdrawComposer(id, currency, amount)` 9314 | invented id + extra currency int |
| 3680 | `package_95/class_3094` save settings (`ChestSettingsUI.as:211`) | int chestId, string name, string desc, bool everyoneCanOpen, bool everyoneCanDonate, int stateMode, int previewMode, int previewAmount, bool wiredEnabled | **3274** (L986) | `ChestSavePreferencesEvent` 9315 without the 9th bool | `ChestSaveSettingsComposer` 9315 (8 fields) | invented id; 9th bool missing. The Wired upgrade is this save with wiredEnabled=true (`ChestSettingsUI.confirmUpgrade`) |
| - | none | - | - | `ChestEnableWiredEvent` 9345 | `ChestEnableWiredComposer` 9345 | **no AIR message**; dropped, folded into 3274 |
| 1786 | `package_95/class_3275` save options (`WiredChestWrapperView.as:573`) | int chestId, bool locked, bool autoLock, int capacity | **1211** (L646) | `ChestSaveOptionsEvent` 9338 (same shape) | `ChestSaveOptionsComposer` 9338 | invented id only |
| 2402 | `package_95/class_2747` save notifications (`ChestNotificationSettingsUI.as:111`) | int chestId, **int notifyMode**, bool chestFull, bool donation, bool someoneWithdraws, bool chestEmpty, bool wiredTransaction | **3937** (L1102) | `ChestSaveNotificationsEvent` 9316: 5 bools then mode | `ChestSaveNotificationsComposer` 9316: 5 bools then mode | invented id + wrong order |
| 1940 | `package_95/class_3721` upgrade capacity (`WiredChestUpgradeConfirmationView.as:82`) | int chestId, int amount | **2964** (L926) | `ChestUpgradeEvent` 9317 (same shape) | `ChestUpgradeCapacityComposer` 9317 | invented id only |
| 540 | `package_95/class_3508` lock chests (`WiredMenuChestsTab.as:142-162`) | bool lock, bool all | **1323** (L664) | `WiredChestLockEvent` 9329 | `WiredChestLockComposer` 9329 | invented id. AIR expects no reply (time-out spinner) |
| 1287 | `package_186/class_2956` wired trade offer (`WiredTradingModel.as:204,218`) | bool remove, int count, int[] itemIds | **1774** (L737) | `WiredChestOfferItemsEvent` 9335 (same shape) | `WiredTradeOfferItemsComposer` 9335 | invented id only |
| 2276 | `package_186/class_3394` wired trade accept (`WiredTradingModel.as:227,246`) | bool confirm | **609** (L1155) | `WiredChestAcceptEvent` 9336 | `WiredTradeAcceptComposer` 9336 | invented id only |
| 1474 | `package_186/class_3259` wired trade cancel (`WiredTradingModel.as:289`) | (empty) | **1898** (L754) | `WiredChestCancelEvent` 1898 | `WiredTradeCancelComposer` 1898 | already official |
| 3170 | `package_193/class_3888` get contract (`WiredContractController.as:68`) | int contractId | **3415** (L1010) | `GetWiredContractContentsEvent` 3415 | `GetWiredContractContentsComposer` 3415 | already official |
| 413 | `package_193/class_3028` update contract (`WiredContractController.as:125`) | contract (class_2536 shape) | **2559** (L865) | `UpdateWiredContractEvent` 2559 | `UpdateWiredContractComposer` 2559 | already official |
| 1441 | `package_92/class_3079` chest transaction logs (`WiredChestWrapperView.as:227`) | int logListId, int pageSize, int page | **3922** (L1097) | not handled | `ChestRequestLogComposer` 9318 | **not implemented server-side**; out of scope (see Unresolved) |
| 235 | `package_92/class_2381` room transaction logs (`WiredMenuChestsTab.as:137`) | int pageSize, int page | **88** (L1201) | not handled | `WiredChestRoomLogsComposer` 9328 | not implemented server-side; out of scope |
| 1997 | `package_92/class_2623` transaction details (`TransactionTableObject.as:59`) | long transactionId | **1697** (L724) | not handled | `WiredChestTransactionDetailsComposer` 9334 | not implemented server-side; out of scope |
| - | none | - | - | `ChestDepositInventoryItemEvent` 9325 | `ChestDepositInventoryItemComposer` 9325 | **no AIR message**: AIR deposits only through the wired trade (3268 -> 1774 -> 609). Dropped |
| - | none | - | - | `ChestDepositCoinsEvent` 9313 (wallet credits) | `ChestDepositComposer` 9313 | **no AIR message**: coins enter a chest as credit furni offered in the trade. Dropped |
| - | none | - | - | - | `ChestDepositFurniComposer` 9321 | no AIR message, no server handler. Dropped |

## Server -> client

| AIR 131318 id | Class / parser (131318) | Fields (AIR order) | Old 121709 id (map line) | PlusEMU before | Volt-Renderer before | Gap / fix |
|---|---|---|---|---|---|---|
| 3327 | `package_112/class_3066` / `wiredtrading/chests/class_2435` | int chestId. Client answers with open chest (1595) (`WiredChestController.onOpenChest`, L129) | **3966** (L1722) | not sent; furni use opened directly | `ChestOpenEvent` 9327 (unused) | interactor now sends it |
| 1541 | `package_112/class_2436` / `chests/class_4267` | int chestId, bool isNotificationPreferences. Success only: hides the settings frame (`ChestSettingsUI.as:126`, `ChestNotificationSettingsUI.as:73`) | **2159** (L1428) | `WiredChestSettingsAckComposer` 9347 (chestId, saved) | `WiredChestSettingsAckEvent` 9347 | invented id + different meaning |
| 3362 | `package_112/class_3153` / `chests/class_4243` | int chestId, int resultCode (0 = success) | **1373** (L1293) | `WiredChestUpgradeComposer` 9335 (same shape) | `ChestUpgradeResultEvent` 9335 | invented id only |
| 419 | `package_112/class_3799` / `chests/class_4324` | int chestId, int coins, bool isUpdate (false opens the coin view, true updates the open one) | **988** (L1834) | inside `WiredChestContentsComposer` 9312 | inside `ChestDataEvent` 9312 | split out |
| 1688 | `package_112/class_2610` / `chests/class_4334` | int chestId, int totalFragments, int fragmentNo, int count, ChestStorage[] | **44** (L1734) | `WiredChestFurniChunkComposer` 9322 (same shape) | `ChestFurniChunkEvent` 9322 | invented id only. Only accepted while the client is opening that chest |
| 2725 | `package_112/class_3289` / `chests/class_4133` | int chestId, int n, int[] removedIds, int m, ChestStorage[] added | **518** (L1751) | not sent | `ChestFurniDeltaEvent` 9323 | now sent to open viewers after a transfer. NB: 518 is a *different* message (transaction fail) in 131318 |
| - | none | - | - | `WiredChestContentsComposer` 9312 | `ChestDataEvent` 9312 | **no AIR message**. AIR reads chest settings from the furni's map stuff data (`WiredChestWrapperView.getStuffDataMap`, L384) and computes rights client-side (L643-655). Dropped |
| - | none | - | - | `WiredChestLockComposer` 9329 | `WiredChestLockStateEvent` 9329 | no AIR message; dropped (state arrives via stuff data) |
| 518 | `package_196/class_3447` / `package_139/class_2958` | int failureReason (`wired_transactions.notification.fail.N`) | **1360** (L1290) | `WiredChestTradeEndComposer` 9333 on failures | none | invented id. Now used for transaction failures outside an open trade window (failed reward, refused withdraw, a deposit that cannot start) |
| 3394 | `package_196/class_3152` / `package_139/class_3638` -> `WiredTransactionSuccessContents` | int successType (0 deposit, 1 withdraw, 2 reward, 3 payment, 4 trade); if type 2: rule (int n, TradeRequirementNode[]), string rewardText, bool openByDefault | **125** (L1269) | `WiredChestRewardComposer` 9346 (rule, text, open; Volt node shape) | `WiredChestRewardEvent` 9346 | invented id, missing type, node shape |
| 147 | `package_192/class_3890` / `trade/class_2961` | int tradeErrorId (`wired_transactions.notification.trade_error.N`) | **503** (L1748) | not sent | none | now sent when an offer is refused |
| 1959 | `package_192/class_3295` / `trade/class_3527` | TradeRequirement, bool showRequirementsImmediate, bool overridePreviousTrade, int timeoutSeconds | **84** (L1812) | `WiredChestTradeOpenComposer` 9331 (own layout) | `WiredTradeOpenEvent` 9331 | invented id + layout |
| 3900 | `package_192/class_3020` / `trade/class_3109` | class_3481 trading items (first = user, second = chest side), bool canAccept, int extra | **705** (L1784) | `WiredChestTradeItemsComposer` 9332 (own layout) | `WiredTradeItemsEvent` 9332 | invented id + layout |
| 2320 | `package_192/class_3825` / `trade/class_2924` | int failureReason; closes the trade window | **2553** (L1489) | `WiredChestTradeEndComposer` 9333 | `WiredTradeCancelledEvent` 2553 | server used invented 9333 |
| 555 | `package_192/class_3864` / `trade/class_3987` | (empty); closes the trade window | **1501** (L1319) | `WiredChestTradeEndComposer` 9334 | `WiredTradeCompletedEvent` 1501 | server used invented 9334 |
| 241 | `package_99/class_3713` / `package_131/class_3264` | int contractId | **3833** (L1698) | `WiredContractOpenComposer` 3833 | `WiredContractOpenEvent` 3833 | already official |
| 2057 | `package_99/class_2398` / `package_131/class_2536` | contract | **2796** (L1530) | `WiredContractContentsComposer` 2796 | `WiredContractContentsEvent` 2796 | already official |
| 2687 | `package_99/class_3001` / `package_131/class_3554` | int contractId, bool success, string error | **72** (L1785) | `WiredContractUpdateResultComposer` 72 | `WiredContractUpdateResultEvent` 72 | already official |
| 2272 | `package_196/class_3086` transaction details | WiredTransactionDetails | **3189** (L1589) | not sent | `WiredChestTransactionDetailsEvent` 9330 | not implemented server-side; out of scope |
| 3917 | `package_196/class_3348` transaction logs | class_3283 | **1831** (L1374) | not sent | `ChestLogEvent` 9319 / `WiredChestRoomLogsEvent` 9328 | not implemented server-side; out of scope |
| - | none | - | - | - | `ChestNotificationEvent` 9336 | no AIR message and no server sender |

## Shared structures (131318 = 121709)

- `ChestItemType` (`package_198/ChestItemType.as`): bool wall, int typeId, string legacyPosterId.
- `ChestStorage` (`package_198/ChestStorage.as:20`): int inventoryId, int lockState, long transactionId,
  ChestItemType, bool groupable, int specialType, stuff data, `if (!wall)` int extra.
- `TradeRequirementNode` (`package_231/TradeRequirementNode.as`): **byte** type (0 coins, 1 furni), int amount,
  `if furni` ChestItemType.
- `TradeRequirementRule`: int n, node[n].
- `TradeRequirement` (`.../trade/requirements/TradeRequirement.as`): int type (0 any coins, 1 any furni,
  2 any, 4 rules), string youGetText, string layoutType, `if type == 4` TradeRequirementRules.
- `TradeRequirementRules`: TradeRequirementRulesDefinition (bool hasGive, [int n, rule[n]], bool hasGet, [rule]),
  int rulesType (0 normal, 1 multiplier, 2 auto), `if 1` int multiplier, `if 2` int autoMultiplierMax.
- `class_3481` (`package_122/class_3481.as`): int firstUserId, int n, item[n], int firstUserNumItems,
  int firstUserNumCredits, int secondUserId, int m, item[m], int secondUserNumItems, int secondUserNumCredits.
- trade item `class_2591` (`package_141/class_2591.as`): int itemId, string type ("I" wall / "S" floor),
  int roomItemId, int typeId, int category, bool groupable, stuff data, int creationDay, int creationMonth,
  int creationYear, `if "S"` int extra.

Chest stuff data keys the AIR client reads (`furniture_data` map): `chest_name`, `chest_desc`,
`everyone_can_open`, `everyone_can_donate`, `state_control_mode`, `preview_mode`, `preview_amount`,
`is_wired_enabled`, `locked`, `auto_lock`, `capacity`, `capacity_level` (`ChestSettingsUI.as:182-191`,
`WiredChestWrapperView.as:360-527`) and `notification_chest_full`, `notification_donation`,
`notification_someone_withdraws`, `notification_chest_empty`, `notification_wired_transaction`,
`notify_mode` (`ChestNotificationSettingsUI.as:92-98`). PlusEMU already wrote the first group; the
notification keys are added.

## PlusEMU behaviour on the official wire (branch fix/chest-protocol-air)

- Furni use of a chest sends `WiredChestOpen` (3966) when the user may open it; the client's `ChestOpen` (1595) gets
  `ChestCoins(isUpdate=false)` (988) for a coin chest or `ChestFurniChunk` (44) fragments for a furni chest.
- Viewers of an open chest get `ChestCoins(isUpdate=true)` or `ChestFurniUpdate` (518, removed ids + added storages)
  whenever the chest's stuff data is refreshed with different contents (per-viewer last-shown state).
- Settings / notification saves answer `ChestSettingsUpdated` (2159) only when saved; the stuff data is refreshed either
  way. `ChestSavePreferences` (3274) reads the 9th bool; `WiredEnabled = old || wired` (one-way), the store still refuses
  it for `_starter` chests and refuses any disable.
- Wired trade: `WiredTradeOpen` (84) sends a plain deposit / donation-mode payment as requirement type 0/1/2 by chest
  kinds, a contract with rules as type 4 with **unscaled** rules, rules type = box mode (0/1/2) and the multiplier.
  `WiredTradeItems` (705): first user = offered inventory items (credits = Exchange furni value), second user = the
  first matching chest items for the scaled reward plus reward coins, canAccept = requirement met, extra = times met.
  Refused offers answer `WiredTradeError` (503: 0 invalid item, 1 too many items).
- A finished trade sends `WiredTradeCompleted` (1501) then `WiredTransactionSuccess` (125) type 0 deposit / 3 payment /
  4 trade; a failed one `WiredTradeCancelled` (2553, reason). Rewards send 125 type 2 with the rule. Withdrawals send
  125 type 1 or `WiredTransactionFail` (1360, reason) instead of a free-text notification.
- `WiredChestLock` (1323) has no answer; chests' stuff data shows the new state.
- A withdrawal the chest refuses answers `WiredTransactionFail` (1360) with 12 (not a Wired chest) or 15 (locked / no
  rights) instead of staying silent.
- Using a chest furni only answers `WiredChestOpen`; it does not fire the furni state-changed Wired trigger (the
  renderer used to open chests directly, and AIR has not shown that trigger firing on a chest use).

## Unresolved

- Transaction logs (`3922`, `88`, `1697` out; `3189`, `1831` in, 121709 ids) exist in both builds but PlusEMU
  has no transaction log store. Volt-Renderer keeps its 93xx log composers/parsers untouched; they never get
  an answer from PlusEMU. Porting them needs a server log table first (separate task).
- `ChestNotificationEvent` 9336 (Volt-Renderer) has no AIR counterpart and no server sender. Left alone.
- No message was missing from the 121709 build: every chest/transaction/contract class of 131318 exists in
  121709 with an identical body.
