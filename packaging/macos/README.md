# RepoGalaxy for macOS

The release script publishes an Apple Silicon self-contained app bundle and a compressed DMG:

```bash
bash scripts/publish-release-macos.sh
```

The resulting artifacts are placed in `release/1.0.0-preview.1-osx-arm64/`. The app uses the macOS Keychain for credentials and stores its local data under `~/Library/Application Support/RepoGalaxy`.

The release is ad-hoc signed and is not notarized. A Developer ID certificate and Apple notarization credentials are required for a Gatekeeper-verified distribution.
