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

The application creates a fresh `repogalaxy-v2.db` under the platform local application-data directory. It does not read or migrate the former local database.

## GitHub sign-in

Device Flow is the default sign-in method. On Windows, credentials are encrypted with the current user's DPAPI. The optional browser loopback flow is shown only when `REP0GALAXY_GITHUB_CLIENT_SECRET` is configured on the local machine (`REPOGALAXY_GITHUB_CLIENT_SECRET` is also accepted for compatibility); never embed that secret in source code or distribution artifacts.

When signed out, RepoGalaxy makes one automatic Trending search per application session, then serves its local cache until the user explicitly refreshes.

## Synchronization

Discovery sync runs only while the application is open. Its default interval is 30 minutes. Subscription queries are capped, candidate feeds are deduplicated by repository and source, and saved repositories only produce a release item for a new non-prerelease release ID.

See [product design](Design/README.md) and [roadmap](Design/ROADMAP.md).
