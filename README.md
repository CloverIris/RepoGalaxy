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

```powershell
dotnet restore
dotnet build RepoGalaxy.sln
dotnet test RepoGalaxy.sln
dotnet run --project src/RepoGalaxy.Desktop
```

The application creates a fresh `repogalaxy-v2.db` under the platform local application-data directory. It does not read or migrate the former local database.

## Synchronization

Discovery sync runs only while the application is open. Its default interval is 30 minutes. Subscription queries are capped, candidate feeds are deduplicated by repository and source, and saved repositories only produce a release item for a new non-prerelease release ID.

See [product design](Design/README.md) and [roadmap](Design/ROADMAP.md).
