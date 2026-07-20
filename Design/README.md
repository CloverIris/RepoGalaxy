# RepoGalaxy product design

## Core promise

Help a developer continually discover worthwhile GitHub projects and turn their interest into a readable feed, an organized library, and timely updates.

## Information architecture

The shell contains Discover, Subscriptions, Library, Notifications, My Repositories, Local Repositories, and Settings.

Discover has three sources: For you, Subscriptions, and Trending. Its home composition contains three five-item boards (daily growth, new projects, and local-stack relevance), a stable two-column card grid, and a 300px contextual rail. The rail shows a 365-day local contribution heatmap, official GitHub RSS items, release activity, and cache/sync health; selecting a repository replaces it with details.

Subscriptions are explicit topic, language, and keyword rules. The library data model supports collection names, tags, and notes. Notifications surface high-match discoveries and a saved repository's new stable release while the desktop process is alive.

## Visual direction

Use Avalonia's built-in Fluent controls, system light/dark mode, compact high-density cards, and a focused detail pane. At 1280px and above the rail is inline; at medium widths it becomes a drawer; below 1000px the feed becomes one column. The retired visual language is not part of the product.

## Data and lifecycle

The v3 SQLite database is a new migrated store. No v2 bookmarks, history, preferences, or local repository data is imported. SQLite runs in WAL mode with foreign keys, a five-second busy timeout, full synchronous writes, UTC Unix-millisecond instants, daily backups and integrity recovery.

The cache is local-only: byte-bounded L1 LRU plus compressed, quota-limited SQLite response entries. Stale values remain usable for up to seven days during network failures and refresh through a per-key single-flight coordinator.

Authenticated startup validates `/user`, then resumes owned and starred repository pagination, capped subscription searches, rotating release checks, and ranking rebuilds. All GitHub requests use a single-concurrency priority orchestrator. Sign-out first cancels protected work, then deletes private repositories, relations, account checkpoints, private cache entries and derived rankings.

Ranking is explainable and local. Coarse ranking weighs rule match, freshness, star velocity, quality and user affinity; fine ranking adds feedback, novelty and local-stack relevance. Results are diversified, reserve 15% stable exploration positions, and persist their algorithm version, feature snapshot, scores, position and impression feedback.
