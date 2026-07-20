# RepoGalaxy roadmap

## Current foundation

- .NET 10 and Avalonia 12.1.0
- Official Fluent theme with no third-party UI kit
- Feed, subscription, saved-repository, and release-notification persistence
- Incremental discovery sync with subscription search, trending candidates, deduplication, and stable release checks
- Standard desktop shell and list/detail discovery workflow

## Next increments

1. Complete first-run interest onboarding and persist the user's topic, language, and keyword profile.
2. Surface collection, tag, and note editing in the Library UI.
3. Add a Windows 11 native notification transport behind `IDesktopNotificationService`, including activation-to-detail navigation.
4. Expand sync resilience coverage with deterministic rate-limit and backoff tests.
