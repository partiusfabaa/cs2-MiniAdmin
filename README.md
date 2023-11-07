# cs2-MiniAdmin
Adds basic administrator functions

# Installation
1. Install [CounterStrike Sharp](https://github.com/roflmuffin/CounterStrikeSharp) and [Metamod:Source](https://www.sourcemm.net/downloads.php/?branch=master)
3. Download [MiniAdmin](https://github.com/partiusfabaa/cs2-MiniAdmin/releases/tag/v1.0.0)
4. Unzip the archive and upload it to the game server

# Commands
## For administrators:
`css_who`, `!who` - shows all players, admin and game time(server console only)

`css_slay "userid"`, `!slay "userid"` - allows you to kill a player

`css_kick "userid"`, `!kick "userid"` - allows you to kick a player from the server

`css_map "name_map"`, `!map "name_map"` - allows you to change the map on the server

`css_ban "userid" "time_minutes or 0 - permanently" "reason"`,

`!ban "userid" "time_minutes or 0 - permanently" "reason"` - Bans a player

`css_unban "steamid" "reason"`, `!unban "steamid" "reason"` - unbans the player 

(example SteamId: STEAM_0:1:123456)

## For chief administrators:
`css_addadmin "username" "steamid" "time_minutes or 0 - permanently"`, 

`!addadmin "username" "steamid" "time_minutes or 0 - permanently"` - Adds an administrator(server console only)

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
