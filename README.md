# ASF-FreeGames

[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0) [![Plugin-ci](https://github.com/Berisch/ASFFreeGames/actions/workflows/ci.yml/badge.svg)](https://github.com/Berisch/ASFFreeGames/actions/workflows/ci.yml) [![Github All Releases](https://img.shields.io/github/downloads/Berisch/ASFFreeGames/total.svg)]()

## Description

ASF-FreeGames is a **[plugin](https://github.com/JustArchiNET/ArchiSteamFarm/wiki/Plugins)** for **[ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm)** allowing one to automatically **collect free steam games** 🔑 posted on [Reddit](https://www.reddit.com/user/ASFinfo?sort=new).

> This is a maintained fork of [maxisoft/ASFFreeGames](https://github.com/maxisoft/ASFFreeGames). It provides separate builds for ASF 6 and ASF 7, and the plugin updates itself from this fork's releases.

---

## Requirements

- ✅ a working [ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm) environment

## Installation

1. Open the [v1.11.0 release](https://github.com/Berisch/ASFFreeGames/releases/tag/v1.11.0) and download `ASFFreeGames-V6.zip` for ASF 6 or `ASFFreeGames-V7.zip` for ASF 7.
2. Extract the ZIP into `plugins/ASFFreeGames/` in your ASF installation, replacing the previous plugin DLL if prompted.
3. Start or restart ASF.

> ⚠️ Keep the plugin in its own folder. ASF installs plugin updates by replacing everything in the plugin's folder, so a dll placed directly in `plugins/` would take the other plugins down with it on the first update.

### Upgrading from ASF 6 to ASF 7

If you're running v1.10.1, install the [v1.10.2 bridge release](https://github.com/Berisch/ASFFreeGames/releases/tag/v1.10.2) while still on ASF 6. Use the plugin updater or extract `ASFFreeGames-generic.zip` into `plugins/ASFFreeGames/`. Restart ASF 6 so it loads the bridge before upgrading ASF itself. The bridge can then select the ASF 7 package when ASF performs the upgrade.

If you've already upgraded to ASF 7 with the old plugin, manually extract `ASFFreeGames-V7.zip` into `plugins/ASFFreeGames/` and restart ASF. For ASF 7's `generic` distribution, install the .NET 11 runtime required by ASF.

## How does it work

Every ⏰`30 minutes` the plugin starts 🔬analyzing [reddit](https://www.reddit.com/user/ASFinfo?sort=new) for new **free games**⚾.
Then every 🔑`addlicense asf appid` command found is broadcasted to each currently **logged bot** 💪.

The list is fetched once per run and shared by all bots. Sources are tried one after another:
1. the [ASFinfo Reddit feed](https://www.reddit.com/user/ASFinfo.rss?sort=new) (RSS, the JSON API is blocked for unauthenticated clients)
2. the [gist](https://gist.github.com/C4illin/e8c5cf365d816f2640242bf01d8d3675) maintained by the ASFinfo bot itself (no free to play / DLC flags)
3. public [redlib](https://github.com/redlib-org/redlib) instances, as a last resort

After ASF starts, the first run waits until every enabled bot is logged on (at most 5 minutes). A bot that logs on later is caught up once using the already fetched list.

## Commands

- `freegames` to collect free games right now 🚀
- `getip` to get the IP used by ASF 👀
- `set` to configure this plugin's options (see below) 🛠️

For information about issuing 📢commands see [ASF's wiki](https://github.com/JustArchiNET/ArchiSteamFarm/wiki)

### Advanced configuration

The plugin behavior is configurable via command

- `freegames set nof2p` to ⛔**prevent** the plugin from collecting **free to play** games
- `freegames set f2p` to ☑️**allow** the plugin to collect **f2p** (the default)
- `freegames set nodlc` to ⛔**prevent** the plugin from collecting **dlc**
- `freegames set dlc` to ☑️**allow** the plugin to collect **dlc** (the default)

In addition to the commands above, the configuration is stored in a 📖`config/freegames.json.config` JSON file, which one may 🖊 edit using a text editor to suit their needs.

## Proxy Setup

The plugin can be configured to use a proxy (HTTP(S), SOCKS4, or SOCKS5) for its HTTP requests to Reddit. You can achieve this in two ways:

1. **Environment Variable:** Set the environment variable `FREEGAMES_RedditProxy` with your desired proxy URL (e.g., `http://yourproxy:port`).
2. **`freegames.json.config`:** Edit the `redditProxy` property within the JSON configuration file located at `<asf>/config/freegames.json.config`. Set the value to your proxy URL.

**Example `freegames.json.config` with Proxy:**

```json
{
...
  "redditProxy": "http://127.0.0.1:1080"
}
```

**Important Note:** If you pass a proxy **password**, it will be **stored in clear text** in the `freegames.json.config` file, even when passing it via the environment variable.

**Note:** Whichever method you choose (environment variable or config file), only one will be used at a time.
The environment variable takes precedence over the config file setting.

## FAQ

### Log is full of `Request failed after 5 attempts!` messages is there something wrong ?

- There's nothing wrong (most likely), those error messages are the result of the plugin trying to add a steam key which is unavailable. With time those errors should occurs less frequently (see [#3](https://github.com/maxisoft/ASFFreeGames/issues/3) for more details).

### How to configure automatic updates for the plugin?

The plugin supports checking for updates on GitHub. You can enable automatic updates by modifying the `PluginsUpdateList` property in your ArchiSteamFarm configuration (refer to the [ArchiSteamFarm wiki](https://github.com/JustArchiNET/ArchiSteamFarm/wiki/Configuration#pluginsupdatelist) for details).

GitHub's **Latest** release stays on v1.10.2 with its single ASF 6 archive, `ASFFreeGames-generic.zip`, so v1.10.1 installations can update to the bridge. Starting with the bridge, the updater checks stable release tags and selects the package for the target ASF major version. For a manual install, use the [v1.11.0 release](https://github.com/Berisch/ASFFreeGames/releases/tag/v1.11.0).

**Important note:** Enabling automatic updates for plugins can have security implications. It's recommended to thoroughly test updates in a non-production environment before enabling them on your main system.

------

## Dev notes

### Compilation

Run these commands from the repository root in PowerShell. Use the .NET 10 SDK for the ASF 6 build and the .NET 11 SDK for the ASF 7 build.

```powershell
git submodule update --init --recursive
./.github/scripts/Publish-Plugin.ps1 -ASFVersion 6

$asf7Tag = (Get-Content .github/asf-v7-tag -Raw).Trim()
git clone --branch $asf7Tag --recurse-submodules https://github.com/JustArchiNET/ArchiSteamFarm.git out/ArchiSteamFarm-V7
./.github/scripts/Publish-Plugin.ps1 -ASFVersion 7
```

The script writes `out/ASFFreeGames-V6.zip` and `out/ASFFreeGames-V7.zip`. If you already have the matching ASF sources in another directory, pass that path with `-ASFSourceDir`.

### Release smoke tests

Extract each archive into `plugins/ASFFreeGames/` in a separate ASF installation. Use ASF 6.3.11.7 for the V6 package and ASF 7.0.0.9 for V7. Start each host, confirm the plugin loads in the ASF log, and run `FREEGAMES RELOAD` to verify that the command is recognized and reloads the configuration.

[![GitHub sponsor](https://img.shields.io/badge/GitHub-sponsor-ea4aaa.svg?logo=github-sponsors)](https://github.com/sponsors/maxisoft)
