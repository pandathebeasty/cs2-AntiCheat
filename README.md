# CS2-AntiCheat

A modular, server-side anti-cheat for Counter-Strike 2 built on
[CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp). Inspired by
[SourceMod Anti-Cheat (SMAC)](https://github.com/Silenci0/SMAC).

Each detection is a self-contained **module** with its own config file and its own punishment action
(none / log / kick / ban), so you can tune, disable, or escalate each check independently. Violations
can be mirrored to Discord and to the CounterStrikeSharp log file.

> Author: **panda** · Version: **1.0.0** · Target: **.NET 10 / CounterStrikeSharp API ≥ 374**

---

## Detection modules

| Module            | What it looks for                                                        | Config file                  |
|-------------------|--------------------------------------------------------------------------|------------------------------|
| **Aimbot**        | Suspicious aim snaps / angle changes toward targets                      | `Aimbot.json`                |
| **Backtrack**     | Hitting players at positions they no longer occupy                       | `Backtrack.json`             |
| **BunnyHop**      | Automated / scripted bunnyhopping patterns                               | `BunnyHop.json`              |
| **RapidFire**     | Firing a semi-auto weapon faster than humanly possible                   | `RapidFire.json`             |
| **UntrustedAngles** | View angles outside the range the engine should ever produce           | `UntrustedAngles.json`       |
| **ImpossibleYaw** | Yaw deltas that are physically impossible between ticks                  | `ImpossibleYaw.json`         |
| **DuckSpam**      | Crouch-spam (duck toggling) beyond human rates                           | `DuckSpam.json`              |
| **UserCmdRate**   | Abnormal user-command rates (command injection / timing cheats)          | `UserCmdRate.json`           |
| **NoRecoil**      | Recoil compensation that no human input could achieve                    | `NoRecoil.json`              |
| **AntiDLL**       | Cheat DLLs that subscribe to blacklisted engine game events              | `AntiDLL.json` (+ gamedata)  |

All modules are enabled by default. Each writes its own `Configs/plugins/CS2-AntiCheat/<Module>.json`
on first run.

---

## Installation

1. Install [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) (Metamod + CSSharp)
   and make sure it loads.
2. Copy the **CS2-AntiCheat** folder into:
   ```
   game/csgo/addons/counterstrikesharp/plugins/
   ```
   so you end up with `.../plugins/CS2-AntiCheat/CS2-AntiCheat.dll`.
3. Start the server (or `css_plugins load CS2-AntiCheat`). Config files are generated on first load.
4. Check `addons/counterstrikesharp/logs/` — you should see the load lines (see **Logging** below).

**Platforms:** Windows and Linux are both supported. The AntiDLL module uses per-platform
signatures/offsets shipped in `gamedata/CS2-AntiCheat.AntiDLL.json`.

---

## Configuration

### General (`Configs/plugins/CS2-AntiCheat/General.json`)

| Key                   | Default                                             | Description                                                                 |
|-----------------------|-----------------------------------------------------|-----------------------------------------------------------------------------|
| `Enabled`             | `true`                                              | Master on/off switch for all detections.                                    |
| `Debug`               | `false`                                             | Verbose runtime logging to the console/log. Load milestones log regardless. |
| `LogPrefix`           | `[CS2AC]`                                            | Prefix prepended to every console log line. Empty = no prefix.              |
| `CommandPermission`   | `@css/root`                                          | Permission flag required to run admin commands. Server console always passes. |
| `BanCommand`          | `css_ban #{id} 0 "ANTICHEAT: {reason}"`             | Command run (server console) when a module's action is **Ban**.             |
| `KickCommand`         | `css_kick #{id}`                                    | Command run after the ban command (also used for **Kick**).                 |
| `DiscordAlerts`       | `true`                                              | Master switch for Discord detection alerts.                                 |
| `DiscordWebhookUrl`   | `""`                                                | Discord webhook URL. Empty = Discord disabled.                              |
| `DiscordMentionRoleId`| `""`                                                | `""` = no ping · `everyone` · `here` · a numeric role id.                   |

Command placeholders: `{id}` (userid — use `#{id}` to target by userid), `{steamid}` (SteamID64),
`{steamid2}` (STEAM_0:…), `{name}`, `{reason}`, `{module}`.

### Per-module (`Configs/plugins/CS2-AntiCheat/<Module>.json`)

Every module config has at least:

| Key                | Values                                  | Description                                            |
|--------------------|-----------------------------------------|--------------------------------------------------------|
| `DetectionEnabled` | `true` / `false`                        | Turn this single module on or off.                     |
| `DetectionAction`  | `0` none · `1` log · `2` kick · `3` ban | What to do when this module detects a player.          |

Individual modules add their own thresholds/tunables on top of those two keys.

### AntiDLL (`Configs/plugins/CS2-AntiCheat/AntiDLL.json`)

AntiDLL detects cheat DLLs by the engine game-event listeners they register — cheats subscribe to
events a normal client never listens to.

| Key                | Default        | Description                                                              |
|--------------------|----------------|--------------------------------------------------------------------------|
| `DetectionEnabled` | `true`         | Enable AntiDLL.                                                          |
| `DetectionAction`  | `3` (Ban)      | Action on detection. Listening to a blacklisted event is unambiguous.    |
| `Blacklist`        | ~160 events    | Game-event names that flag a client. Edit freely — no recompile needed.  |

The native **signatures/offsets** live in `gamedata/CS2-AntiCheat.AntiDLL.json` (editable, no
recompile). If a game update breaks them, update that file. See **How AntiDLL works** below.

---

## Commands

All admin commands require `CommandPermission` (default `@css/root`); the **server console always
passes**.

| Command                      | Args              | Description                                                   |
|------------------------------|-------------------|---------------------------------------------------------------|
| `tbac_reload`                | —                 | Reload `General.json` (re-applies `Enabled`, `Debug`, prefix).|
| `tbac_aimbot_action`         | `0‑3`             | Set the Aimbot module's action.                               |
| `tbac_backtrack_action`      | `0‑3`             | Set the Backtrack module's action.                            |
| `tbac_bunnyhop_action`       | `0‑3`             | Set the BunnyHop module's action.                             |
| `tbac_rapidfire_action`      | `0‑3`             | Set the RapidFire module's action.                            |
| `tbac_untrustedangles_action`| `0‑3`             | Set the UntrustedAngles module's action.                      |
| `tbac_impossibleyaw_action`  | `0‑3`             | Set the ImpossibleYaw module's action.                        |
| `tbac_duckspam_action`       | `0‑3`             | Set the DuckSpam module's action.                             |
| `tbac_usercmdrate_action`    | `0‑3`             | Set the UserCmdRate module's action.                          |
| `tbac_norecoil_action`       | `0‑3`             | Set the NoRecoil module's action.                             |
| `tbac_antidll_action`        | `0‑3`             | Set the AntiDLL module's action.                              |

Action values: `0` = none · `1` = log · `2` = kick · `3` = ban. `*_action` commands write the new
value to that module's config.

---

## Logging

Load milestones are **always** written to `addons/counterstrikesharp/logs/` (independent of `Debug`):

```
[CS2AC] Loaded CS2-AntiCheat v1.0.0
[CS2AC] Initialized 10 detection module(s): Aimbot, Backtrack, BunnyHop, RapidFire, UntrustedAngles, ImpossibleYaw, DuckSpam, UserCmdRate, NoRecoil, AntiDLL
[CS2AC] AntiDLL blacklist mask built (152/160 events resolved to bits)
```

The AntiDLL "mask built" line is the key health check: `X/Y resolved` with `X > 0` means AntiDLL
successfully translated blacklisted event names into listen-bit indices for your game build. `0`
means the gamedata signatures are stale for your current CS2 version.

Verbose runtime logs (per-player events, ticks, detections) are gated by `"Debug": true` in
`General.json` so the log file stays clean in production. Change it and run `tbac_reload`.

---

## How AntiDLL works

When a client connects it tells the server which game events it wants to listen to (a
`CLCMsg_ListenEvents` message carrying a bitmask of event indices). Legitimate clients only ever
subscribe to a small, fixed set; many cheat DLLs subscribe to extra engine events to drive their
features. AntiDLL flags any client that listens to an event in the `Blacklist`.

Implementation (fully self-contained, ported from
[KillStr3aK/CS2-AntiDLL](https://github.com/KillStr3aK/CS2-AntiDLL)):

1. On the event manager's init, each blacklisted event **name** is resolved to its **listen-bit
   index** via `CGameEventManager::GetEventDescriptor`, building a 512-bit blacklist mask once.
2. `CSource1LegacyGameEventGameSystem::ListenBitsReceived` is hooked. On each message, the client's
   listen-bits are read straight from the message and `AND`-ed with the mask — any set bit is a
   blacklisted event the client subscribed to.

No per-event lookups and no proxy-listener walk at runtime. Signatures/offsets are in
`gamedata/CS2-AntiCheat.AntiDLL.json` with separate Windows and Linux entries:

| Key                                               | Purpose                                                         |
|---------------------------------------------------|-----------------------------------------------------------------|
| `CSource1LegacyGameEventGameSystem::ListenBitsReceived` | Function hooked to intercept a client's listen-bits.      |
| `CLCMsg_ListenEvents::EventCount` / `EventData`   | Offsets of the event-bit count + data pointer in the message.   |
| `CGameEventManager::GetEventDescriptor`           | Resolves an event name to its listen-bit index (at init).       |

All native reads are wrapped in `try/catch`; if a signature is stale the module stays idle and logs
it instead of crashing the server.

---

## Building from source

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). All dependencies come
from NuGet (`CounterStrikeSharp.API`).

```bash
dotnet build CS2-AntiCheat.csproj -c Release
```

The compiled plugin is produced at `bin/Release/net10.0/CS2-AntiCheat.dll` along with the `gamedata/`
and `lang/` assets it needs.

---

## Credits

- [AlliedModders — original SMAC](https://forums.alliedmods.net/forumdisplay.php?f=133)
- [Silenci0 — SMAC fork](https://github.com/Silenci0/SMAC)
- [roflmuffin — CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)
- [KillStr3aK — CS2-AntiDLL](https://github.com/KillStr3aK/CS2-AntiDLL) (AntiDLL module basis)
