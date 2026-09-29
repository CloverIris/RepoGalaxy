# RepoGalaxy for Linux

Build an x86_64 AppImage on Ubuntu 22.04 or a compatible Linux host with the .NET 10 SDK and `appimagetool` installed:

```bash
bash scripts/publish-release-linux.sh
```

The AppImage and its SHA-256 checksum are published to `release/1.0.0-preview.1-linux-x64/`. The package is self-contained and targets x86_64. Linux desktop graphics libraries provided by the host system are still required.

Linux Secret Service credential storage is not implemented yet, so GitHub sign-in cannot save credentials in this preview; guest mode remains available.
