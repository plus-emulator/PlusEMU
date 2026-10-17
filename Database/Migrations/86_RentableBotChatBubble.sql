-- Rentable bots talk with chat bubble 31 ("Bot Rentable") on Habbo. Observed on hhus (AIR WIN63-202610131318-860371703): a bought
-- Robbie bot's Wired Bot Talk line arrives with bubble style 31. Bought bots got the table default 2 ("Robot"). Pets keep theirs.
-- Idempotent.

UPDATE `bots` SET `chat_bubble` = 31 WHERE `chat_bubble` = 2 AND `ai_type` <> 'pet';
