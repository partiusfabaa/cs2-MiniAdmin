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
| `css_admin`, `!admin` | opens the admin menu | - |
| `css_ban "#userid or username" "time_minutes or 0 - permanently" [reason]` | Bans a player | Ban |
| `css_unban "steamid" "reason"`, `!unban "steamid" [reason]` | Unbans the player | Unban |
| `css_mute <#userid or username> <time_seconds> [reason]` | Turns off player voice chat | Generic |
| `css_unmute <steamid or #userid> [reason]` | Enables voice chat for the player | Unban |
| `css_gag <#userid or username> <time_seconds> [reason]` | Disables player chat | Generic |
| `css_ungag <steamid or #userid> [reason]` | Enables player chat | Unban |
| `css_slay "#userid or username"`, `!slay "userid"` | Allows you to kill a player | Slay |
| `css_kick "#userid or username"`, `!kick "userid"` | Allows you to kick a player from the server | Kick |
| `css_map "name_map"`, `!map "name_map"` | Allows you to change the map on the server | ChangeMap |
| `css_team <#userid or username> <ct/tt/spec/none> [-k]` | Allows you to transfer a player for another team, if you write `-k` to the team the player will die | Kick |
| `css_say <message>` | Allows you to send a message to the chat room | Chat |
| `css_csay <message>` | Allows you to send a message to the CENTER | Chat |
| `css_psay <#userid or username> <message>` | Allows you to send a private message | Chat |
| `css_cvar <cvar> <value>` | Change cvar value | Cvar |
| `css_god [#userid or username]` | Allows you to grant immortality, if you don't write userid or name, it will be granted to the one who wrote it. | Cheats |
| `css_rcon <command>` | Changes cvar with rcon | Rcon |
| `css_slap <#userid or username> [damage]` | Slap a player | Slay |
| `css_who`, `!who` | Shows all players, admin, and game time | - |

To write to admin chat, you need to open Team chat and write @MYTEXT

## A LOT OF THESE FLAGS DON'T WORK. THEY HAVE BEEN ADDED FOR THE FUTURE.
### FLAGS:
    Reservation = a (doesn't work)
    Generic  = b
    Kick  = c
    Ban  = d
    Unban  = e
    Slay = f
    Changemap  = g
    Cvar  = h
    Config = i (doesn't work)
    Chat = j
    Vote = k (doesn't work)
    Password = l (doesn't work)
    Rcon = m
    Cheats = n
    Vip  = o (doesn't work)
    Root = z

(example SteamId: STEAM_0:1:123456)

## For chief administrators:
`css_addadmin "username" "steamid" "immunity" "flags" "time_minutes or 0 - permanently"`, - Adds an administrator(server console only)

`css_deleteadmin "steamid"` - Removes the administrator(server console only) 

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
