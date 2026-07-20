# RepoGalaxy

RepoGalaxy is a desktop GitHub discovery and subscription client. It helps developers turn promising repositories into a manageable reading feed, saved library, and release-aware notification stream.

## Product

- **Discover**: separate For you, Subscriptions, and Trending feeds with an explanation for every item.
- **Subscriptions**: discover projects through topic, language, and keyword rules.
- **Library**: save repositories for later, with collection, tag, and note data ready in the local model.
- **Notifications**: track unread discoveries and saved-repository stable releases.
- **Workspaces**: retain My Repositories and Local Repositories as standard list-based workspaces.

The product intentionally uses Avalonia's official Fluent theme only. It follows the system theme by default; no third-party UI library is installed.

## Development

Requirements: .NET SDK 10 and the .NET 10 desktop runtime.

For Visual Studio debugging, use **Visual Studio 2026 18.8 or later** with the
**.NET desktop development** workload. The repository is pinned to SDK
`10.0.302` in `global.json`; Visual Studio 2022 must not be used for this
solution because it cannot reliably load the .NET 10 SDK.

Open `RepoGalaxy.slnx` with the Visual Studio 2026 instance. If Windows has
associated solution files with Visual Studio 2022, use **Open with** and select:
`C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe`.
Set `RepoGalaxy.Desktop` as the startup project and press F5.

```powershell
dotnet restore
dotnet build RepoGalaxy.slnx
dotnet test RepoGalaxy.slnx
dotnet run --project src/RepoGalaxy.Desktop
```

The application creates a fresh `repogalaxy-v3.db` under the platform local application-data directory. EF Core migrations are applied under a cross-process lock. Before schema upgrades the database is backed up, and a daily backup set retains the newest seven files. A former v2 database and its WAL/SHM files are moved into a seven-day legacy backup instead of being imported.

## GitHub sign-in

Device Flow is the default sign-in method. On Windows, a single credential envelope is encrypted with the current user's DPAPI. The optional browser loopback flow is shown only when `REPOGALAXY_GITHUB_CLIENT_SECRET` is configured on the local machine (`REP0GALAXY_GITHUB_CLIENT_SECRET` is accepted only as a compatibility alias); never embed that secret in source code or distribution artifacts. Every credential is verified through `/user` before the UI reports a successful login.

When signed out, RepoGalaxy makes one automatic Trending search per application session, then serves its local cache until the user explicitly refreshes.

## Synchronization

Discovery sync runs only while the application is open. Its default interval is 30 minutes and can be changed to 15/30/60 minutes or manual-only. GitHub requests pass through a single-consumer priority queue, use response rate-limit headers, and persist page checkpoints. Subscription queries are capped at two pages per pass; saved repository release checks rotate in batches of twenty.

Network responses use a byte-bounded in-memory LRU cache backed by compressed SQLite entries. Reads implement fresh/stale/miss semantics with single-flight stale-while-revalidate. The Settings page controls memory and disk quotas, TTLs and refresh interval without deleting bookmarks, subscriptions or reading history.

The recommendation path persists candidate batches, feature vectors, coarse and fine scores, explanations, positions and impressions. It performs a stable two-stage rank, diversity rerank and 15% deterministic exploration. The Discover page presents three top-five boards, a responsive two-column grid and a right rail for local Git contributions, official GitHub news, releases and data health.

See [product design](Design/README.md) and [roadmap](Design/ROADMAP.md).
