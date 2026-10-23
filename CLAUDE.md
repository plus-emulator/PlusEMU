# PlusEMU - agent instructions

## Writing

- Write in plain language (ISO 24495-1): short sentences, common words, active voice. Use a plain hyphen, never an em or en dash.
- Say what the code or change does, as in "Fixes ..." or "Adds ...". "just like Habbo" is fine.
- Never write where or how a behavior was observed or proven: no client builds, AIR paths, run or firing counts, rooms, test sessions, probe or trace names. That evidence belongs in the task report, not in the repository.

## Code comments

- The fewer the better. Add one only when it explains a rule or reason the code cannot show.
- State the rule, as in `// A blob changes state on the next room tick.`, in one or two short sentences.
- No history, evidence, PR numbers or "who found it".

## Commits and pull requests

- Titles use [Conventional Commits](https://www.conventionalcommits.org/): `type(scope): summary`, e.g. `fix(wired): re-stack a furni_to_furni mover already on its target`.
- Wrap code names in backticks in titles, descriptions, commit messages and release notes, for example `` `@state` `` or `` `furni_to_furni` ``. GitHub turns a bare `@name` into a mention of the GitHub user with that name, and release notes are built from PR titles. Before you publish a release, check its notes: every `@` outside backticks must be a PR author.
- Keep descriptions short: what was wrong and what changed.
- Cite Habbo evidence as "Observed on AIR `<build>`" or with the AIR path and line. Don't name rooms, runs or test sessions.
- No test counts, suite results, lint or code-style status, checklists or long tables.
- No "Generated with" footers, no AI co-author trailers and no mention of an AI or a tool.
- Never mention other emulators or clients (for example Turbo or Habz) in titles, descriptions, commit messages or code comments. Only official Habbo behaviour is evidence.
