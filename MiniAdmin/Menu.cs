using System;
using System.Linq;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;
using static BaseAdmin.BaseAdmin;

namespace BaseAdmin;

public class Menu
{
    private readonly BaseAdmin _baseAdmin;
    private readonly ChatMenu _slayMenu;
    private readonly ChatMenu _kickMenu;
    private readonly ChatMenu _muteMenu;
    private readonly ChatMenu _banMenu;
    private readonly ChatMenu _changeMapMenu;
    private readonly ChatMenu _addAdminMenu;

    public Menu(BaseAdmin baseAdmin)
    {
        _baseAdmin = baseAdmin;

        _slayMenu = new ChatMenu(baseAdmin.Prefix);
        _kickMenu = new ChatMenu(baseAdmin.Prefix);
        _muteMenu = new ChatMenu(baseAdmin.Prefix);
        _banMenu = new ChatMenu(baseAdmin.Prefix);
        _changeMapMenu = new ChatMenu(baseAdmin.Prefix);
        _addAdminMenu = new ChatMenu(baseAdmin.Prefix);
    }

    public void CreateMenu()
    {
        var adminMenu = new ChatMenu(_baseAdmin.Prefix);
        adminMenu.AddMenuOption("Slay players", (player, _) =>
        {
            if (_baseAdmin.CheckingForAdminAndFlag(player, AdminFlag.Slay))
            {
                CreateSlayMenu();
                ChatMenus.OpenMenu(player, _slayMenu);
            }
        });
        adminMenu.AddMenuOption("Kick players", (player, _) =>
        {
            if (_baseAdmin.CheckingForAdminAndFlag(player, AdminFlag.Kick))
            {
                CreateKickMenu();
                ChatMenus.OpenMenu(player, _kickMenu);
            }
        });
        adminMenu.AddMenuOption("Mute players", (player, _) =>
        {
            if (_baseAdmin.CheckingForAdminAndFlag(player, AdminFlag.Generic))
            {
                CreateMuteMenu();
                ChatMenus.OpenMenu(player, _muteMenu);
            }
        });
        adminMenu.AddMenuOption("Ban players", (player, _) =>
        {
            if (_baseAdmin.CheckingForAdminAndFlag(player, AdminFlag.Ban))
            {
                CreateBanMenu();
                ChatMenus.OpenMenu(player, _banMenu);
            }
        });
        adminMenu.AddMenuOption("Change map", (player, _) =>
        {
            if (_baseAdmin.CheckingForAdminAndFlag(player, AdminFlag.Changemap))
            {
                CreateChangeMapMenu();
                ChatMenus.OpenMenu(player, _changeMapMenu);
            }
        });
        adminMenu.AddMenuOption("Add Admin", (player, _) =>
        {
            if (_baseAdmin.CheckingForAdminAndFlag(player, AdminFlag.Root))
            {
                CreateAddAdminMenu();
                ChatMenus.OpenMenu(player, _addAdminMenu);
            }
        });

        _baseAdmin.AddCommand("css_admin", "admin menu", (player, info) =>
        {
            if (player == null) return;
            if (!_baseAdmin.IsAdmin(player.Index))
            {
                _baseAdmin.PrintToChat(player, "you do not have access to this command");
                return;
            }

            ChatMenus.OpenMenu(player, adminMenu);
        });
    }

    private void CreateSlayMenu()
    {
        var playerEntities =
            Utilities.GetPlayers().Where(u => u.PlayerPawn.Value != null && u.PlayerPawn.Value.IsValid);
        _slayMenu.MenuOptions.Clear();
        _slayMenu.AddMenuOption("Pick a player", (_, _) => { }, true);
        _slayMenu.AddMenuOption("All", (controller, option) =>
        {
            foreach (var player in playerEntities)
                if (player.PlayerPawn.Value != null)
                    player.PlayerPawn.Value.CommitSuicide(true, true);
        });
        foreach (var player in playerEntities)
        {
            if (!player.PawnIsAlive) continue;

            _slayMenu.AddMenuOption($"{player.PlayerName} [{player.Index}]", (controller, option) =>
            {
                var parts = option.Text.Split('[', ']');
                if (parts.Length < 2) return;
                var target = Utilities.GetPlayerFromIndex(int.Parse(parts[1]));

                if (!target.PawnIsAlive)
                {
                    _baseAdmin.PrintToChat(controller, "The player is already dead");
                    return;
                }

                if (target.PlayerPawn.Value != null)
                    target.PlayerPawn.Value.CommitSuicide(true, true);

                _baseAdmin.PrintToChatAll($"{controller.PlayerName}: Player '{target.PlayerName}' has been killed");
            });
        }
    }

    private void CreateKickMenu()
    {
        _kickMenu.MenuOptions.Clear();
        _kickMenu.AddMenuOption("Pick a player", (_, _) => { }, true);
        foreach (var player in Utilities.GetPlayers())
        {
            _kickMenu.AddMenuOption($"{player.PlayerName} [{player.Index}]", (controller, option) =>
            {
                var parts = option.Text.Split('[', ']');
                if (parts.Length < 2) return;
                var target = Utilities.GetPlayerFromIndex(int.Parse(parts[1]));

                if (!target.IsValid)
                {
                    _baseAdmin.PrintToChat(controller, "This player is invalid");
                    return;
                }

                //_baseAdmin.KickClient($"{target.UserId}");
                target.Kick("Kick");

                _baseAdmin.PrintToChatAll($"{controller.PlayerName}: Player '{target.PlayerName}' has been kicked");
            });
        }
    }

    private void CreateBanMenu()
    {
        _banMenu.MenuOptions.Clear();
        _banMenu.AddMenuOption("Pick a player", (_, _) => { }, true);
        foreach (var player in Utilities.GetPlayers())
        {
            _banMenu.AddMenuOption($"{player.PlayerName} [{player.Index}]", (controller, option) =>
            {
                var parts = option.Text.Split('[', ']');
                if (parts.Length < 2) return;
                var target = Utilities.GetPlayerFromIndex(int.Parse(parts[1]));

                if (!target.IsValid)
                {
                    _baseAdmin.PrintToChat(controller, "This player is invalid");
                    return;
                }

                SelectionReasonsForBanning(controller, target);
            });
        }
    }

    private void SelectionReasonsForBanning(CCSPlayerController player, CCSPlayerController target)
    {
        var reasonMenu = new ChatMenu(_baseAdmin.Prefix);
        reasonMenu.AddMenuOption("Pick a reason", (_, _) => { }, true);
        foreach (var reason in _baseAdmin.ReadAllBanReasons())
        {
            reasonMenu.AddMenuOption(reason, (controller, option) =>
            {
                var timeMenu = new ChatMenu(_baseAdmin.Prefix);
                timeMenu.AddMenuOption("Select a time", (_, _) => { }, true);
                foreach (var time in _baseAdmin.ReadAllTime())
                {
                    var split = time.Split(':');
                    timeMenu.AddMenuOption(split[0],
                        (playerController, _) =>
                        {
                            AddBanFromMenu(playerController, target, option.Text, int.Parse(split[1]));
                        });
                }

                ChatMenus.OpenMenu(controller, timeMenu);
            });
        }

        ChatMenus.OpenMenu(player, reasonMenu);
    }

    private void AddBanFromMenu(CCSPlayerController controller, CCSPlayerController target, string reason,
        int time)
    {
        var startBanTimeUnix = DateTime.UtcNow.GetUnixEpoch();
        var endBanTimeUnix = DateTime.UtcNow.AddSeconds(time).GetUnixEpoch();

        var msg = _baseAdmin.Database.AddBan(new User
        {
            admin_username = controller.PlayerName,
            admin_steamid = new SteamID(controller.SteamID).SteamId2,
            username = target.PlayerName,
            steamid64 = target.SteamID,
            steamid = new SteamID(target.SteamID).SteamId2,
            reason = reason,
            unban_reason = string.Empty,
            admin_unlocked_username = string.Empty,
            admin_unlocked_steamid = string.Empty,
            start_ban_time = startBanTimeUnix,
            end_ban_time = time == 0 ? time : endBanTimeUnix,
            ban_active = true
        }).Result;
        //_baseAdmin.KickClient($"{target.UserId}");
        target.Kick("Ban");
        _baseAdmin.PrintToChatAll(msg);
    }

    private void CreateMuteMenu()
    {
        _muteMenu.MenuOptions.Clear();
        _muteMenu.AddMenuOption("Pick a player", (_, _) => { }, true);
        foreach (var player in Utilities.GetPlayers())
        {
            _muteMenu.AddMenuOption($"{player.PlayerName} [{player.Index}]", (controller, option) =>
            {
                var parts = option.Text.Split('[', ']');
                if (parts.Length < 2) return;
                var target = Utilities.GetPlayerFromIndex(int.Parse(parts[1]));

                if (!target.IsValid)
                {
                    _baseAdmin.PrintToChat(controller, "This player is invalid");
                    return;
                }

                SelectionTypeMute(controller, target);
            });
        }
    }

    private void SelectionTypeMute(CCSPlayerController player, CCSPlayerController target)
    {
        var reasonMenu = new ChatMenu(_baseAdmin.Prefix);
        reasonMenu.AddMenuOption("Pick a type", (_, _) => { }, true);
        reasonMenu.AddMenuOption("All", (_, _) => { SelectionReasonsForMute(player, target, MuteType.All); });
        reasonMenu.AddMenuOption("Mute",
            (_, _) => { SelectionReasonsForMute(player, target, MuteType.Micro); });
        reasonMenu.AddMenuOption("Gag",
            (_, _) => { SelectionReasonsForMute(player, target, MuteType.Chat); });
        ChatMenus.OpenMenu(player, reasonMenu);
    }

    private void SelectionReasonsForMute(CCSPlayerController player, CCSPlayerController target,
        MuteType muteType)
    {
        var reasonMenu = new ChatMenu(_baseAdmin.Prefix);
        reasonMenu.AddMenuOption("Pick a reason", (_, _) => { }, true);
        foreach (var reason in _baseAdmin.ReadAllMuteReasons())
        {
            reasonMenu.AddMenuOption(reason, (controller, option) =>
            {
                var timeMenu = new ChatMenu(_baseAdmin.Prefix);
                timeMenu.AddMenuOption("Select a time", (_, _) => { }, true);
                foreach (var time in _baseAdmin.ReadAllTime())
                {
                    var split = time.Split(':');
                    timeMenu.AddMenuOption(split[0],
                        (playerController, _) =>
                        {
                            AddMuteFromMenu(playerController, target, option.Text, int.Parse(split[1]), muteType);
                        });
                }

                ChatMenus.OpenMenu(controller, timeMenu);
            });
        }

        ChatMenus.OpenMenu(player, reasonMenu);
    }

    private void AddMuteFromMenu(CCSPlayerController controller, CCSPlayerController target, string reason,
        int time, MuteType muteType)
    {
        var startMuteTimeUnix = DateTime.UtcNow.GetUnixEpoch();
        var endMuteTimeUnix = DateTime.UtcNow.AddSeconds(time).GetUnixEpoch();

        Server.NextFrame(() =>
        {
            _baseAdmin.Database.AddMute(new MuteUser
            {
                mute_type = (int)muteType,
                admin_username = controller != null ? controller.PlayerName : "Console",
                admin_steamid = controller != null ? new SteamID(controller.SteamID).SteamId2 : "Console",
                username = target.PlayerName,
                steamid64 = target.SteamID,
                steamid = new SteamID(target.SteamID).SteamId2,
                reason = reason,
                unmute_reason = "",
                admin_unlocked_username = "",
                admin_unlocked_steamid = "",
                start_mute_time = startMuteTimeUnix,
                end_mute_time = endMuteTimeUnix == 0 ? 0 : endMuteTimeUnix,
                mute_active = true
            }, controller == null ? null : controller);

            if (muteType is MuteType.Micro or MuteType.All)
                target.VoiceFlags = VoiceFlags.Muted;
        });

        _baseAdmin.UpdateUserMuteLocal(target, time, endMuteTimeUnix, (int)muteType);
    }

    private void CreateChangeMapMenu()
    {
        _changeMapMenu.MenuOptions.Clear();
        _changeMapMenu.AddMenuOption("Pick a map", (_, _) => { }, true);
        foreach (var map in _baseAdmin.GetMapFromMaps())
        {
            var mapsStarts = map.StartsWith("ws:");
            var mapName = mapsStarts ? map.Trim('w', 's', ':') : map;
            _changeMapMenu.AddMenuOption($"{mapName}",
                (controller, option) =>
                {
                    _baseAdmin.PrintToChatAll($"{controller.PlayerName}: changing the map to {mapName}");
                    _baseAdmin.AddTimer(3.0f, () => _baseAdmin.ChangeMap(mapName, mapsStarts));
                });
        }
    }

    private void CreateAddAdminMenu()
    {
        _addAdminMenu.MenuOptions.Clear();
        _addAdminMenu.AddMenuOption("Pick a player", (_, _) => { }, true);
        foreach (var player in Utilities.GetPlayers())
        {
            if (_baseAdmin.IsAdmin(player.Index)) continue;

            _addAdminMenu.AddMenuOption($"{player.PlayerName} [{player.Index}]",
                (controller, option) =>
                {
                    var parts = option.Text.Split('[', ']');
                    if (parts.Length < 2) return;
                    var target = Utilities.GetPlayerFromIndex(int.Parse(parts[1]));

                    if (!target.IsValid)
                    {
                        _baseAdmin.PrintToChat(controller, "This player is invalid");
                        return;
                    }

                    SelectionTimeAndImmunityForAddAdmin(controller, target);
                });
        }
    }

    private void SelectionTimeAndImmunityForAddAdmin(CCSPlayerController player, CCSPlayerController target)
    {
        var timeMenu = new ChatMenu(_baseAdmin.Prefix);
        timeMenu.AddMenuOption("Select a time", (_, _) => { }, true);
        foreach (var time in _baseAdmin.ReadAllTime())
        {
            var split = time.Split(':');
            timeMenu.AddMenuOption(split[0], (playerController, _) =>
            {
                var immunityMenu = new ChatMenu(_baseAdmin.Prefix);
                immunityMenu.AddMenuOption("Choice of immunity", (_, _) => { }, true);
                foreach (var immunity in _baseAdmin.ReadAllImmunity())
                {
                    immunityMenu.AddMenuOption(immunity,
                        (client, _) =>
                        {
                            AddAdminFromMenu(client, target, int.Parse(immunity), int.Parse(split[1]));
                        });
                }

                ChatMenus.OpenMenu(playerController, immunityMenu);
            });
        }

        ChatMenus.OpenMenu(player, timeMenu);
    }

    private void AddAdminFromMenu(CCSPlayerController client, CCSPlayerController target,
        int immunity, int time)
    {
        var startTimeUnix = DateTime.UtcNow.GetUnixEpoch();
        var endTimeUnix = DateTime.UtcNow.AddSeconds(time).GetUnixEpoch();

        _baseAdmin.Database.AddAdmin(new Admins
        {
            username = target.PlayerName,
            steamid = new SteamID(target.SteamID).SteamId2,
            start_time = startTimeUnix,
            end_time = time == 0 ? time : endTimeUnix,
            immunity = immunity,
            flags = "s"
        }, client, true);
    }
}