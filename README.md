# IF YOU FIND A BUG, PLEASE TEXT ME AT DISCORD: thesamefabius

# cs2-MiniAdmin
Adds basic administrator functions

# Installation
1. Install [CounterStrike Sharp](https://github.com/roflmuffin/CounterStrikeSharp) and [Metamod:Source](https://www.sourcemm.net/downloads.php/?branch=master)
3. Download [MiniAdmin](https://github.com/partiusfabaa/cs2-MiniAdmin/releases/tag/v1.0.0)
4. Unzip the archive and upload it to the game server

# Commands
## For administrators:
| Command | Description | Admin Flag |
|---------|-------------|------------|
| `css_who`, `!who` | Shows all players, admin, and game time (server console only) | - |
| `css_slay "#userid or username"`, `!slay "userid"` | Allows you to kill a player | SLAY |
| `css_kick "#userid or username"`, `!kick "userid"` | Allows you to kick a player from the server | KICK |
| `css_map "name_map"`, `!map "name_map"` | Allows you to change the map on the server | MAP  |
| `css_ban "#userid or username" "time_minutes or 0 - permanently" "reason"` | Bans a player | BAN |
| `css_unban "steamid" "reason"`, `!unban "steamid" "reason"` | Unbans the player | UNBAN |
| `css_slap <#userid or username> <damage>` | Slap a player | SLAY |
| `css_gag <#userid or username> <time_seconds> <reason>` | Disables player chat | MUTE |
| `css_ungag <steamid> <reason>` | Enables player chat | UNBAN |
| `css_mute <#userid or username> <time_seconds> <reason>` | Turns off player voice chat | MUTE |
| `css_unmute <steamid> <reason>` | Enables voice chat for the player | UNBAN |
| `css_rcon <command>` | Changes cvar with rcon | RCON |

### FLAGS:
    Ban = a
    Unban = b
    Mute = c
    AdminChat = d
    Slay = e
    Kick = f
    Map = g
    Rcon = h
    Root = z

(example SteamId: STEAM_0:1:123456)

## For chief administrators:
`css_addadmin "username" "steamid" "immunity" "flags" "time_minutes or 0 - permanently"`, 

`!addadmin "username" "steamid" "immunity" "flags" "time_minutes or 0 - permanently"` - Adds an administrator(server console only)

`css_deleteadmin "steamid"`, `!deleteadmin "steamid"` - Removes the administrator(server console only) 

(example SteamId: STEAM_0:1:123456)

# Configs
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
