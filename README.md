# CS2 Mini Admin
Adds basic administrator functions.

### Note: Simple fork, with dumb modifications to `BaseAdmin for cssharp flags`. Use at your own expense, no support for this version

## Installation
1. Install [CounterStrike Sharp](https://github.com/roflmuffin/CounterStrikeSharp) and [Metamod:Source](https://www.sourcemm.net/downloads.php/?branch=master)
3. Download [MiniAdmin](https://github.com/partiusfabaa/cs2-MiniAdmin/releases/tag/v1.0.0)
4. Unzip the archive and upload it to the game server

## Commands
### For administrators:
| Command | Description | Admin Flag |
|---------|-------------|------------|
| `css_admin`, `!admin` | opens the admin menu | - |
| `css_ban "#userid or username" "time_minutes or 0 - permanently" [reason]` | Bans a player | BAN |
| `css_unban "steamid" "reason"`, `!unban "steamid" [reason]` | Unbans the player | UNBAN |
| `css_mute <#userid or username> <time_seconds> [reason]` | Turns off player voice chat | GENERIC |
| `css_unmute <steamid or #userid> [reason]` | Enables voice chat for the player | UNBAN |
| `css_gag <#userid or username> <time_seconds> [reason]` | Disables player chat | GENERIC |
| `css_ungag <steamid or #userid> [reason]` | Enables player chat | UNBAN |
| `css_slay "#userid or username"`, `!slay "userid"` | Allows you to kill a player | SLAY |
| `css_kick "#userid or username"`, `!kick "userid"` | Allows you to kick a player from the server | KICK |
| `css_map "name_map"`, `!map "name_map"` | Allows you to change the map on the server | CAHNGEMAP  |
| `css_rcon <command>` | Changes cvar with rcon | RCON |
| `css_slap <#userid or username> [damage]` | Slap a player | SLAY |
| `css_who`, `!who` | Shows all players, admin, and game time | - |

To write to admin chat, you need to open Team chat and write @MYTEXT

## Flags:
	Reservation = a
	Generic  = b
	Kick  = c
	Ban  = d
	Unban  = e
	Slay = f
	Changemap  = g
	Cvar  = h
	Config = i
	Chat = j
	Vote = k
	Password = l
	Rcon = m
	Cheats = n
	Vip  = o
	Root = z

## Access root
- `css_addadmin "username" "steamid" "immunity" "flags" "time_minutes or 0 - permanently"`, `!addadmin "username" "steamid" "immunity" "flags" "time_minutes or 0 - permanently"` - Adds an administrator ***(server console only)***
- `css_deleteadmin "steamid"`, `!deleteadmin "steamid"` - Removes the administrator ***(server console only)***

(Example SteamId: STEAM_0:1:123456)

## Configs
The configuration is generated automatically next to the plugin dll
```
database.json
{
  "Connection": {
    "Host": 	"HOST",
    "Database": "NAME_DATABASE",
    "User": 	"NAME_USER",
    "Password": "PASSWORD"
  }
}

maps.txt
You just put the name of your maps here.
```
