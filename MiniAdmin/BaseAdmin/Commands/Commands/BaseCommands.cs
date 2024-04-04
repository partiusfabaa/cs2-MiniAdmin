using System;
using System.Threading.Tasks;
using BaseAdminApi;
using BaseAdminApi.Enums;
using BaseAdminApi.Models;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;

namespace BaseAdmin.Commands.Commands;

public class BaseCommands
{
    private readonly BaseAdmin _baseAdmin;

    public BaseCommands(BaseAdmin baseAdmin)
    {
        _baseAdmin = baseAdmin;
    }

    [RegisterCommand("css_admin", AdminFlag.Generic)]
    public void OnCmdOpenMenu(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null) return;

        _baseAdmin.Menu.OpenMenu(controller);
    }

    [RegisterCommand("css_noclip", AdminFlag.Cheats)]
    public void OnCmdNoclip(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null) return;

        if (command.ArgCount >= 1)
        {
            if (!Utils.GetPlayer(command.GetArg(1), out var target)) return;

            GiveNoclip(controller, target);
            return;
        }

        GiveNoclip(controller);
    }

    private void GiveNoclip(CCSPlayerController player, CCSPlayerController? target = null)
    {
        var playerPawn = target == null ? player.PlayerPawn.Value : target.PlayerPawn.Value;
        if (playerPawn != null)
            playerPawn.MoveType = playerPawn.MoveType switch
            {
                MoveType_t.MOVETYPE_NOCLIP => MoveType_t.MOVETYPE_WALK,
                MoveType_t.MOVETYPE_WALK => MoveType_t.MOVETYPE_NOCLIP,
                _ => playerPawn.MoveType
            };
        
        _baseAdmin.PrintToChatAll(_baseAdmin.Localizer["give_noclip", player.PlayerName, target == null ? $"{player.PlayerName}" : $"{target.PlayerName}"]);
    }

    [RegisterCommand("css_team", AdminFlag.Kick, 1, "<#userid or username> <ct/tt/spec/none> [-k]")]
    public void OnCmdChangeTeam(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null) return;

        if (!Utils.GetPlayer(command.GetArg(1), out var target))
        {
            _baseAdmin.ReplyToCommand(controller, _baseAdmin.Localizer["player_not_found"]);
            return;
        }

        if (command.ArgCount >= 3)
        {
            var team = _baseAdmin.GetTeam(command.GetArg(2));

            if (command.ArgCount is 4)
                target.ChangeTeam(team);
            else
                target.SwitchTeam(team);

            _baseAdmin.PrintToChatAll(_baseAdmin.Localizer["move_team", target.PlayerName]);
        }
    }

    [RegisterCommand("css_say", AdminFlag.Chat, 1, "<text>")]
    public void OnCmdSay(CCSPlayerController? controller, CommandInfo command)
    {
        _baseAdmin.PrintToChatAll($"{ChatColors.Blue}Admin\x01: {command.ArgString}");
    }

    [RegisterCommand("css_psay", AdminFlag.Chat, 1, "<#userid or username> <message>")]
    public void OnCmdPSay(CCSPlayerController? controller, CommandInfo command)
    {
        if (!Utils.GetPlayer(command.GetArg(1), out var target))
        {
            _baseAdmin.ReplyToCommand(controller, _baseAdmin.Localizer["player_not_found"]);
            return;
        }

        var fromName = controller == null ? _baseAdmin.Config.BanFromConsoleName : controller.PlayerName;
        var message = _baseAdmin.Localizer["private_message", fromName, command.ArgString].Value;
        
        _baseAdmin.PrintToChat(target, message);
        _baseAdmin.ReplyToCommand(controller, message);
    }

    [RegisterCommand("css_csay", AdminFlag.Chat, 1, "<text>")]
    public void OnCmdCSay(CCSPlayerController? controller, CommandInfo command)
    {
        _baseAdmin.PrintToCenterAll($"{command.ArgString}");
    }

    [RegisterCommand("css_cvar", AdminFlag.Cvar, 2, "<cvar> <value>")]
    public void OnCmdCvar(CCSPlayerController? controller, CommandInfo command)
    {
        var cvar = command.GetArg(1);
        var value = command.GetArg(2);

        var conVar = ConVar.Find(cvar);

        if (conVar == null)
        {
            _baseAdmin.ReplyToCommand(controller, _baseAdmin.Localizer["cvar.not_found", cvar]);
            return;
        }

        conVar.SetValue(value);
        _baseAdmin.ReplyToCommand(controller, _baseAdmin.Localizer["cvar.change_cvar", cvar, value]);
    }

    [RegisterCommand("css_rcon", AdminFlag.Rcon, 1, "<command>")]
    public void OnCmdRcon(CCSPlayerController? controller, CommandInfo command)
    {
        Server.ExecuteCommand(command.ArgString);
    }
    
    [RegisterCommand("css_slay", AdminFlag.Slay, 1, "<username or #userid>")]
    public void OnCmdSlay(CCSPlayerController? controller, CommandInfo command)
    {
        if (!Utils.GetPlayer(command.GetArg(1), out var target)) return;
        if (!_baseAdmin.PlayerImmunityComparison(controller, target)) return;
        
        if (!target.PawnIsAlive || target.Team is CsTeam.None or CsTeam.Spectator)
        {
            _baseAdmin.ReplyToCommand(controller, _baseAdmin.Localizer["player.slay.is_invalid"]);
            return;
        }

        var playerPawn = target.PlayerPawn.Value;
        if (playerPawn != null)
        {
            playerPawn.CommitSuicide(true, true);
        }
        
        _baseAdmin.ReplyToCommand(controller,
            $"{(controller != null ? controller.PlayerName : _baseAdmin.Config.BanFromConsoleName)}: Player '{target.PlayerName}' has been killed");
    }


    [RegisterCommand("css_kick", AdminFlag.Kick, 1, "<username or #userid or steamid2> [reason]")]
    public void OnCmdKick(CCSPlayerController? controller, CommandInfo command)
    {
        if (!Utils.GetPlayer(command.GetArg(1), out var target)) return;
        if (!_baseAdmin.PlayerImmunityComparison(controller, target)) return;

        var kickReason = "None";
        if (command.ArgCount > 1)
            kickReason = command.GetArg(2);

        target.Kick(kickReason);
    }

    [RegisterCommand("css_ban", AdminFlag.Ban, 2, "<username or #userid or steamid2> <time> [reason]")]
    public void OnCmdBan(CCSPlayerController? controller, CommandInfo command)
    {
        if (!Utils.GetPlayer(command.GetArg(1), out var target)) return;
        if (!_baseAdmin.PlayerImmunityComparison(controller, target)) return;

        var time = int.TryParse(command.GetArg(2), out var value) ? value : 0;
        var reason = "None";

        if (command.ArgCount > 3)
            reason = command.GetArg(4);

        Task.Run(() => AddBanAsync(controller, target, time, reason));
    }

    public async Task AddBanAsync(CCSPlayerController? admin, CCSPlayerController target, int time, string reason)
    {
        await Server.NextFrameAsync(() =>
        {
            var adminInfo = _baseAdmin.GetNameAndId(admin);

            var targetId = new SteamID(target.SteamID);
            _baseAdmin.Database.AddBan(admin, new BanUser
            {
                admin_username = adminInfo.name,
                admin_steamid = adminInfo.id,
                username = target.PlayerName,
                steamid64 = targetId.SteamId64,
                steamid = targetId.SteamId2,
                reason = reason,
                start_ban_time = DateTime.UtcNow.GetUnixEpoch(),
                end_ban_time = time == 0 ? 0 : DateTime.UtcNow.AddSeconds(time).GetUnixEpoch(),
                ban_active = true
            });
            target.Kick("ban");
        });
    }

    [RegisterCommand("css_unban", AdminFlag.Unban, 1, "<steamid> [reason]")]
    public void OnCmdUnBan(CCSPlayerController? controller, CommandInfo command)
    {
        var steamId = command.GetArg(1);
        var reason = "None";
        if (command.ArgCount > 1)
            reason = command.GetArg(2);

        Task.Run(() => UnBanAsync(controller, steamId, reason));
    }

    public async Task UnBanAsync(CCSPlayerController? admin, string target, string reason)
    {
        await Server.NextFrameAsync(() =>
        {
            var adminInfo = _baseAdmin.GetNameAndId(admin);

            _baseAdmin.Database.UnbanUser(admin, adminInfo.name, adminInfo.id, target, reason);
        });
    }

    [RegisterCommand("css_mute", AdminFlag.Generic, 2, "<#userid or username> <time_seconds> [reason]")]
    public void OnCmdMute(CCSPlayerController? controller, CommandInfo command)
    {
        if (!Utils.GetPlayer(command.GetArg(1), out var target)) return;
        if (!_baseAdmin.PlayerImmunityComparison(controller, target))
        {
            _baseAdmin.ReplyToCommand(controller, _baseAdmin.Localizer["cant_mute_player"]);
            return;
        }
    }
    
    [RegisterCommand("css_map", AdminFlag.Changemap, 1, "<map>")]
    public void OnCmdChangeMap(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null) return;

        var arg1 = command.GetArg(1);
        foreach (var t in _baseAdmin.BaseConfig.Maps)
        {
            var isWorkshopMap = t.Trim().StartsWith("ws:", StringComparison.OrdinalIgnoreCase);

            if (!t.Trim().Contains(arg1.Trim(), StringComparison.OrdinalIgnoreCase)) continue;

            var map = t.StartsWith("ws:") ? t.Split("ws:")[1] : t;
            _baseAdmin.PrintToChatAll(_baseAdmin.Localizer["menu.server.map_change", controller.PlayerName, map]);
            _baseAdmin.ChangeMap(map, isWorkshopMap);
            return;
        }

        _baseAdmin.PrintToChat(controller, _baseAdmin.Localizer["menu.server.doesnt_exist", arg1]);
    }
}