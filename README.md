# ASF-FreeGames

[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0) [![Plugin-ci](https://github.com/Berisch/ASFFreeGames/actions/workflows/ci.yml/badge.svg)](https://github.com/Berisch/ASFFreeGames/actions/workflows/ci.yml) [![Github All Releases](https://img.shields.io/github/downloads/Berisch/ASFFreeGames/total.svg)]()

## Description

ASF-FreeGames is a **[plugin](https://github.com/JustArchiNET/ArchiSteamFarm/wiki/Plugins)** for **[ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm)** allowing one to automatically **collect free steam games** 🔑 posted on [Reddit](https://www.reddit.com/user/ASFinfo?sort=new).

> This is a maintained fork of [maxisoft/ASFFreeGames](https://github.com/maxisoft/ASFFreeGames). It builds against the latest stable ArchiSteamFarm, and the plugin updates itself from this fork's releases.

---

## Requirements

- ✅ a working [ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm) environment

## Installation

- 🔽 Download the latest `ASFFreeGames-generic.zip` from the [release page](https://github.com/Berisch/ASFFreeGames/releases)
- ➡️ Extract it into its own folder, `plugins/ASFFreeGames/`, of your *ArchiSteamFarm* installation
- 🔄 (re)start ArchiSteamFarm

> ⚠️ Keep the plugin in its own folder. ASF installs plugin updates by replacing everything in the plugin's folder, so a dll placed directly in `plugins/` would take the other plugins down with it on the first update.
- 🎉 Have fun

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

**Important note:** Enabling automatic updates for plugins can have security implications. It's recommended to thoroughly test updates in a non-production environment before enabling them on your main system.

------

## Dev notes

### Compilation

Simply execute `dotnet build ASFFreeGames -c Release` and find the dll in `ASFFreeGames/bin` folder, which you can drag to ASF's `plugins` folder.

[![GitHub sponsor](https://img.shields.io/badge/GitHub-sponsor-ea4aaa.svg?logo=github-sponsors)](https://github.com/sponsors/maxisoft)
