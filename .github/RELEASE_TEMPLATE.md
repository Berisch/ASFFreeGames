### Downloads

- ASF 6 with .NET 10 uses `ASFFreeGames-V6.zip`.
- ASF 7 with .NET 11 uses `ASFFreeGames-V7.zip`. Install the .NET 11 runtime if you use ASF 7's `generic` distribution.

Extract the matching ZIP into `plugins/ASFFreeGames/` and restart ASF.

### Upgrading from ASF 6 to ASF 7

If you have ASFFreeGames v1.10.1, install `ASFFreeGames-generic.zip` from the v1.10.2 bridge release while still running ASF 6. Restart ASF 6 to load the bridge before upgrading ASF. The bridge can then select the ASF 7 package during the ASF upgrade. [v1.10.2 bridge release](https://github.com/Berisch/ASFFreeGames/releases/tag/v1.10.2)

If you've already moved to ASF 7, extract `ASFFreeGames-V7.zip` into its own `plugins/ASFFreeGames/` folder, replace the old DLL, and restart ASF.

GitHub's **Latest** release stays on v1.10.2 so the v1.10.1 updater can find its single ASF 6 archive. This stable dual-package release is available directly by tag. [v1.11.0 release](https://github.com/Berisch/ASFFreeGames/releases/tag/v1.11.0)
