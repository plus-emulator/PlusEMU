# Furni-Matic

Furni-Matic uses the native recycler window and Ecotron boxes. Its configuration is stored in `recycler_settings`, `recycler_levels` and `recycler_prizes`. It stays disabled until an operator supplies a valid distribution and enables it.

## Configuration

- `slots`: 1 to 12 eligible furniture instances per exchange.
- `cooldown_seconds`: 0 to 86,400 seconds between exchanges by the same user.
- `cost_duckets`: 0 to 1,000,000 Duckets per exchange. Existing installations keep a cost of zero.
- Levels: up to six distinct levels, numbered 1 to 6. Level 1 must have `chance=1` and receives the remaining probability. Each other level has an absolute probability of `1/chance`. Their sum must be less than one.
- Each level needs at least one prize. Prizes within a level have equal probability. A prize has either an `item_id` or a `chat_style_id`, never both.

For six levels with chances `1, 4, 10, 40, 400, 5000`, the probabilities are 62.23%, 25%, 10%, 2.5%, 0.25% and 0.02%. Invalid or overflowing distributions close the recycler instead of dropping prizes.

Set the client's `recycler.number_of_slots` and `recycler.ducket_cost` properties to the same slot count and cost. These values control the display; the server always validates its own configuration and balance. Client revisions without purchased chat-style messages cannot recycle a distribution containing chat styles.

Chat-style prizes must be enabled, require ownership, and have no HC or permission requirement. Acquired styles belong to the user who opens the box. Opening another box with the same style keeps one permanent right, records another opening and awards no substitute currency or furniture.

## Transactions and recovery

An exchange consumes eligible owned inventory items, charges Duckets, chooses the box contents, updates the cooldown and records progress in one transaction. Limited items, borrowed Builders Club items, active music objects and unopened containers cannot be recycled. The reward is fixed when the box is created.

An opening consumes the placed box and grants its contents in one transaction. A box can be opened once, including after transfer to another owner. Old boxes without receipts remain usable, but do not receive an invented rarity level.

`recycler_operations`, `recycler_openings` and `recycler_revisions` retain immutable receipts. A lost commit response is resolved against the receipt on a new connection. If recovery is unavailable, the session closes and its cached balances are excluded from logout persistence. Login reloads inventory and ownership from storage and reconciles the five recycler achievement totals.

Migrations preserve existing prize rows, cooldowns, pending boxes and chat-style preferences. No reward catalog is enabled automatically. Removing this feature requires disabling new exchanges; keep its receipts and ownership tables.
