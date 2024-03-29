﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace BaseAdmin;

public class BaseAdmin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "Mini Admin";
    public override string ModuleVersion => "v1.0.5";

    private static string _dbConnectionString = string.Empty;

    //MENU
    public readonly string Prefix = "[\x0C Admin Menu \x01]";

    private readonly DateTime[] _playerPlayTime = new DateTime[65];
    private readonly MuteUserLocal?[] _muteUsers = new MuteUserLocal[65];
    private readonly Admins?[] _adminUsers = new Admins[65];

    public Database Database = null!;
    private AdminChat _adminChat = null!;
    private Config _config;

    public enum MuteType
    {
        Micro = 0,
        Chat,
        All
    }

    public enum AdminFlag
    {
        Reservation = 'a',
        Generic = 'b',
        Kick = 'c',
        Ban = 'd',
        Unban = 'e',
        Slay = 'f',
        Changemap = 'g',
        Cvar = 'h',
        Config = 'i',
        Chat = 'j',
        Vote = 'k',
        Password = 'l',
        Rcon = 'm',
        Cheats = 'n',
        Vip = 'o',
        Root = 'z'
    }

    public override void Load(bool hotReload)
    {
        _config = LoadConfig();
        _dbConnectionString = BuildConnectionString();
        Database = new Database(this, _dbConnectionString);
        _adminChat = new AdminChat();
        new Menu(this).CreateMenu();

        Task.Run(Database.CreateTable);
        Task.Run(Database.CreateAdminsTable);
        Task.Run(Database.CreateMuteTable);

        RegisterListener<Listeners.OnClientAuthorized>((slot, id) =>
        {
            OnClientConnectedAsync(slot, Utilities.GetPlayerFromSlot(slot), id);
            OnClientAuthorizedAsync(slot, id);
        });

        RegisterListener<Listeners.OnClientDisconnectPost>(slot =>
        {
            _muteUsers[slot + 1] = null;
            _adminUsers[slot + 1] = null;
            _playerPlayTime[slot + 1] = DateTime.MinValue;
        });

        AddCommandListener("say", CommandListener_Say);
        AddCommandListener("say_team", CommandListener_SayTeam);

        AddTimer(300, () => Task.Run(Database.DeleteExpiredAdminsAsync), TimerFlags.REPEAT);
        AddTimer(60, () =>
        {
            foreach (var player in Utilities.GetPlayers().Where(u => !u.IsBot))
            {
                var user = _muteUsers[player.Index];

                if (user == null) continue;

                var endTime = DateTimeOffset.FromUnixTimeSeconds(user.EndMuteTime).UtcDateTime;

                if (endTime <= DateTime.UtcNow)
                {
                    if (user.MuteType is (int)MuteType.Micro or (int)MuteType.All)
                        player.VoiceFlags = VoiceFlags.Normal;

                    _muteUsers[player.Index] = null;
                }
            }
        }, TimerFlags.REPEAT);
    }

    private HookResult ProcessChatMessage(CCSPlayerController? controller, CommandInfo info, bool sendToAllAdmins)
    {
        if (controller == null) return HookResult.Continue;

        var msg = GetTextInsideQuotes(info.ArgString);
        var entityIndex = controller.Index;

        if (msg.StartsWith('@'))
        {
            if (CheckingForAdminAndFlag(controller, AdminFlag.Chat))
            {
                if (sendToAllAdmins)
                {
                    _adminChat.SendToAllFromAdmin(msg.Trim('@'));
                    return HookResult.Handled;
                }

                foreach (var player in Utilities.GetPlayers()
                             .Where(player => CheckingForAdminAndFlag(player, AdminFlag.Chat)))
                {
                    _adminChat.SendToAdminChat(player,
                        $" {ChatColors.Blue}{controller.PlayerName}{ChatColors.Default}: {msg.Trim('@')}");
                }

                return HookResult.Handled;
            }
            else
            {
                if (!IsUserMute(entityIndex))
                {
                    foreach (var player in Utilities.GetPlayers())
                    {
                        if (!CheckingForAdminAndFlag(controller, AdminFlag.Chat)) continue;

                        _adminChat.SendToAdminChatFromPlayer(player,
                            $" {ChatColors.Blue}{controller.PlayerName}{ChatColors.Default}: {msg.Trim('@')}");
                        return HookResult.Handled;
                    }
                }
            }
        }

        if (IsUserMute(entityIndex))
            return CheckMuteStatus(controller, MuteType.Chat) ? HookResult.Handled : HookResult.Continue;

        return HookResult.Continue;
    }

    private HookResult CommandListener_SayTeam(CCSPlayerController? controller, CommandInfo info)
    {
        return ProcessChatMessage(controller, info, sendToAllAdmins: false);
    }

    private HookResult CommandListener_Say(CCSPlayerController? controller, CommandInfo info)
    {
        return ProcessChatMessage(controller, info, sendToAllAdmins: true);
    }

    // private bool CheckMute(CCSPlayerController controller)
    // {
    //     return CheckMuteStatus(controller, MuteType.Chat, "You cannot write a message because you have chat disabled. End via: ");
    // }
    //
    // private bool CheckMicrophoneMute(CCSPlayerController controller)
    // {
    //     return CheckMuteStatus(controller, MuteType.Micro, "You cannot use the microphone because it is muted. End via: ");
    // }

    private bool CheckMuteStatus(CCSPlayerController controller, MuteType requiredMuteType)
    {
        var user = _muteUsers[controller.Index];

        if (user == null) return false;

        if (user.MuteType != (int)MuteType.All && user.MuteType != (int)requiredMuteType) return false;

        var endTime = DateTimeOffset.FromUnixTimeSeconds(user.EndMuteTime).UtcDateTime;
        var timeEnd = endTime - DateTime.UtcNow;

        var message = requiredMuteType == MuteType.Chat
            ? "You cannot write a message because you have chat disabled. End via: "
            : "You cannot use the microphone because it is muted. End via: ";
        var time =
            $"{(timeEnd.Days == 0 ? "" : $"{timeEnd.Days}d, ")}{timeEnd.Hours:00}:{timeEnd.Minutes:00}:{timeEnd.Seconds:00}";
        PrintToChat(controller, message + time);
        return true;
    }

    private async Task OnClientAuthorizedAsync(int playerSlot, SteamID steamId)
    {
        await LoadAdminUserAsync(steamId, playerSlot);

        var userMuted = await Database.GetActiveMuteAsync(steamId.SteamId2);

        if (userMuted != null)
        {
            _muteUsers[playerSlot + 1] = new MuteUserLocal
            {
                MuteType = userMuted.mute_type,
                SteamId = userMuted.steamid,
                EndMuteTime = userMuted.end_mute_time,
                MuteActive = userMuted.mute_active
            };

            if (userMuted.mute_type is (int)MuteType.Micro or (int)MuteType.All)
            {
                Server.NextFrame(() => Utilities.GetPlayerFromSlot(playerSlot).VoiceFlags = VoiceFlags.Muted);
            }
        }
    }

    public async Task LoadAdminUserAsync(SteamID steamId, int playerSlot)
    {
        var userAdmin = await Database.GetAdminFromDb(steamId.SteamId2);

        if (userAdmin != null)
        {
            _adminUsers[playerSlot + 1] = new Admins
            {
                username = userAdmin.username,
                steamid = userAdmin.steamid,
                start_time = userAdmin.start_time,
                end_time = userAdmin.end_time,
                immunity = userAdmin.immunity,
                flags = userAdmin.flags
            };
        }
    }

    private async Task OnClientConnectedAsync(int slot, CCSPlayerController player, SteamID steamId)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            var unbanUsers = await connection.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM miniadmin_bans WHERE steamid64 = @SteamId64 AND end_ban_time <= @CurrentTime AND ban_active = 1 AND end_ban_time != 0",
                new { CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), steamId.SteamId64 });

            if (unbanUsers != null)
            {
                PrintLogInfo("Unban: {steamid}", unbanUsers.steamid);
                await Database.UnbanUser(_config.BanFromConsoleName, _config.BanFromConsoleName, unbanUsers.steamid, "The deadline has passed");
            }

            var unmuteUsers = await connection.QueryFirstOrDefaultAsync<MuteUser>(
                "SELECT * FROM miniadmin_mute WHERE steamid64 = @SteamId64 AND end_mute_time <= @CurrentTime AND mute_active = 1 AND end_mute_time != 0",
                new { CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), steamId.SteamId64 });

            if (unmuteUsers != null)
            {
                PrintLogInfo("Unmute: {steamid}", unmuteUsers.steamid);
                await Database.UnmuteUser(-1, _config.BanFromConsoleName, _config.BanFromConsoleName, unmuteUsers.steamid, "The deadline has passed");

                if (unmuteUsers.mute_type is (int)MuteType.All or (int)MuteType.Micro)
                    Server.NextFrame(() => player.VoiceFlags = VoiceFlags.Normal);
            }

            await Database.DeleteExpiredAdminsAsync();

            var banUser = await connection.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM miniadmin_bans WHERE steamid64 = @SteamId64 AND ban_active = 1",
                new { steamId.SteamId64 });

            if (banUser != null)
                player.Kick("Ban");
            else
                _playerPlayTime[slot + 1] = DateTime.Now;
        }
        catch (Exception e)
        {
            PrintLogError(e.ToString());
        }
    }

    [CommandHelper(0, "<#userid or username>", CommandUsage.CLIENT_ONLY)]
    [ConsoleCommand("css_noclip")]
    public void OnCmdNoclip(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null || !CheckingForAdminAndFlag(controller, AdminFlag.Cheats)) return;

        var msg = GetTextInsideQuotes(command.ArgString);
        if (msg.StartsWith('#'))
        {
            GiveNoclip(controller, GetPlayerFromUserIdOrName(msg));
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

        PrintToChatAll(
            $"Admin '{player.PlayerName}' has issued a noclip to player '{(target == null ? $"{player.PlayerName}" : $"{target.PlayerName}")}'");
    }

    // [ConsoleCommand("css_refresh_admins")]
    // public void OnCmdRefreshAdmins(CCSPlayerController? controller, CommandInfo command)
    // {
    //     if (controller != null && !CheckingForAdminAndFlag(controller, AdminFlag.Root)) return;
    //
    //     foreach (var players in Utilities.GetPlayers()
    //                  .Where(u => u.AuthorizedSteamID != null && u.PlayerPawn.Value != null))
    //     {
    //         Server.NextFrame(() => LoadAdminUserAsync(players.AuthorizedSteamID, players.Slot));
    //     }
    //
    //     const string msg = "The list of administrators has been successfully reloaded";
    //
    //     ReplyToCommand(controller, msg);
    // }
    //
    // [CommandHelper(1, "<steamid>")]
    // [ConsoleCommand("css_reload_infractions")]
    // public void OnCmdReloadInfractions(CCSPlayerController? controller, CommandInfo command)
    // {
    //     if (controller == null) return;
    //     var target = GetPlayerFromSteamId(command.GetArg(1));
    //
    //     if (target == null) return;
    //
    //     Server.NextFrame(() => LoadAdminUserAsync(target.AuthorizedSteamID, target.Slot));
    //
    //     const string msg = "The list of administrators has been successfully reloaded";
    //
    //     ReplyToCommand(controller, msg);
    // }

    [CommandHelper(1, "<#userid or username> <ct/tt/spec/none> [-k]", CommandUsage.CLIENT_ONLY)]
    [ConsoleCommand("css_team")]
    public void OnCmdChangeTeam(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null || !CheckingForAdminAndFlag(controller, AdminFlag.Kick)) return;

        var target = GetPlayerFromUserIdOrName(command.GetArg(1));

        if (target == null)
        {
            ReplyToCommand(controller, "Player not found");
            return;
        }

        if (command.ArgCount >= 3)
        {
            var team = GetTeam(command.GetArg(2));

            if (command.ArgCount is 4)
                target.ChangeTeam(team);
            else
                target.SwitchTeam(team);

            PrintToChatAll($"An administrator has moved a player {target.PlayerName} to another team");
        }
    }

    private CsTeam GetTeam(string name)
    {
        return name switch
        {
            "ct" => CsTeam.CounterTerrorist,
            "t" => CsTeam.Terrorist,
            "tt" => CsTeam.Terrorist,
            "none" => CsTeam.None,
            _ => CsTeam.Spectator
        };
    }

    [CommandHelper(1, "<text>", CommandUsage.CLIENT_ONLY)]
    [ConsoleCommand("css_say")]
    public void OnCmdSay(CCSPlayerController? controller, CommandInfo command)
    {
        if (!CheckingForAdminAndFlag(controller, AdminFlag.Chat)) return;

        PrintToChatAll($"{ChatColors.Blue}Admin\x01: {command.ArgString}");
    }

    [CommandHelper(1, "<#userid or username> <message>", CommandUsage.CLIENT_ONLY)]
    [ConsoleCommand("css_psay")]
    public void OnCmdPSay(CCSPlayerController? controller, CommandInfo command)
    {
        if (!CheckingForAdminAndFlag(controller, AdminFlag.Chat)) return;

        var target = GetPlayerFromUserIdOrName(command.GetArg(1));

        if (target == null)
        {
            ReplyToCommand(controller, "Player not found");
            return;
        }

        PrintToChat(target,
            $"Private message from - {ChatColors.Blue}{(controller == null ? _config.BanFromConsoleName : controller.PlayerName)}\x01: {command.ArgString}");
        ReplyToCommand(controller,
            $"Private message from - {ChatColors.Blue}{(controller == null ? _config.BanFromConsoleName : controller.PlayerName)}\x01: {command.ArgString}");
    }


    [CommandHelper(1, "<text>", CommandUsage.CLIENT_ONLY)]
    [ConsoleCommand("css_csay")]
    public void OnCmdCSay(CCSPlayerController? controller, CommandInfo command)
    {
        if (!CheckingForAdminAndFlag(controller, AdminFlag.Chat)) return;

        PrintToCenterAll($"{command.ArgString}");
    }

    [CommandHelper(usage: "[#userid or name]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
    [ConsoleCommand("css_god")]
    public void OnCmdGod(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null || !CheckingForAdminAndFlag(controller, AdminFlag.Cheats)) return;

        if (command.ArgCount >= 2)
        {
            var target = GetPlayerFromUserIdOrName(command.GetArg(1));

            if (target == null)
            {
                ReplyToCommand(controller, "Player not found");
                return;
            }

            var targetPawn = target.PlayerPawn.Value;
            if (targetPawn == null) return;

            targetPawn.TakesDamage ^= true;
            PrintToChat(target, "The administrator has given you immortality");
        }
        else
        {
            var playerPawn = controller.PlayerPawn.Value;
            if (playerPawn == null) return;
            playerPawn.TakesDamage ^= true;
            PrintToChat(controller, "You've established immortality");
        }
    }

    [CommandHelper(2, "<cvar> <value>")]
    [ConsoleCommand("css_cvar")]
    public void OnCmdCvar(CCSPlayerController? controller, CommandInfo command)
    {
        if (!CheckingForAdminAndFlag(controller, AdminFlag.Cvar)) return;

        var cvar = command.GetArg(1);
        var value = command.GetArg(2);

        var conVar = ConVar.Find(cvar);

        if (conVar == null)
        {
            ReplyToCommand(controller, $"ConVar: {cvar} not found");
            return;
        }

        conVar.SetValue(value);
    }

    [CommandHelper(1, "<command>", CommandUsage.CLIENT_ONLY)]
    [ConsoleCommand("css_rcon")]
    public void OnCmdRcon(CCSPlayerController? controller, CommandInfo command)
    {
        if (!CheckingForAdminAndFlag(controller, AdminFlag.Rcon)) return;

        Server.ExecuteCommand(command.ArgString);
    }

    [CommandHelper(1, "<#userid or username> [damage]", CommandUsage.CLIENT_ONLY)]
    [ConsoleCommand("css_slap")]
    public void OnCmdSlap(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null || !CheckingForAdminAndFlag(controller, AdminFlag.Slay)) return;

        var player = GetPlayerFromUserIdOrName(command.GetArg(1));

        var damage = 0;
        if (command.ArgCount >= 3)
            damage = int.Parse(command.GetArg(2));

        if (!IsPlayerValid(controller, player, true)) return;

        if (player != null)
        {
            var playerPawn = player.PlayerPawn.Value;

            if (playerPawn == null) return;

            playerPawn.AbsVelocity.X += Random.Shared.Next(100, 200);
            playerPawn.AbsVelocity.Y += Random.Shared.Next(100, 200);
            playerPawn.AbsVelocity.Z += Random.Shared.Next(200, 300);
            playerPawn.Health -= damage;

            var random = Random.Shared.Next(3);

            player.ExecuteClientCommand($"play {GetMusicChoice(random)}");

            if (playerPawn.Health <= 0) playerPawn.CommitSuicide(true, true);
        }
    }

    private string GetMusicChoice(int randomNumber)
    {
        return randomNumber switch
        {
            0 => "sounds/player/damage1.vsnd",
            1 => "sounds/player/damage2.vsnd",
            2 => "sounds/player/damage3.vsnd",
            _ => string.Empty
        };
    }

    [ConsoleCommand("css_who")]
    public void OnCmdWho(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller != null)
            if (!IsAdmin(controller.Index))
            {
                PrintToChat(controller, "you do not have access to this command");
                return;
            }

        var maxNameLength = 0;

        var id = 0;
        foreach (var client in Utilities.GetPlayers()
                     .Where(u => u.PlayerPawn.Value != null && u.PlayerPawn.Value.IsValid))
        {
            var playerName = !string.IsNullOrWhiteSpace(client.PlayerName) ? client.PlayerName : "unknown";
            var playerNameLength = playerName.Length;
            maxNameLength = Math.Max(maxNameLength, playerNameLength);

            var adminStatus = IsAdmin(client.Index) ? "admin " : "player";

            var index = client.Index;
            var playTime = DateTime.Now - _playerPlayTime[index];

            id ++;
            var formattedOutput =
                $"{id,-1} - {playerName,-15} || UserId: {client.UserId,-2} || {adminStatus,-6} || Playtime: {playTime.Hours:D2}:{playTime.Minutes:D2}:{playTime.Seconds:D2} | {(client.AuthorizedSteamID == null ? "none" : client.AuthorizedSteamID)}";

            if (controller == null)
            {
                Console.ForegroundColor = Random.Shared.Next(2) == 1 ? ConsoleColor.Cyan : ConsoleColor.DarkMagenta;
                Console.WriteLine(formattedOutput);
                Console.ResetColor();
            }
            else
                PrintToConsole(controller, formattedOutput);

            // PrintLogInfo("{id} - {playerName} | {adminStatus} | Playtime: {playTime} | {SteamID}",
            //     $"{id,-1}", $"{playerName,-15}", $"{adminStatus,-6}",
            //     $"{playTime.Hours:D2}:{playTime.Minutes:D2}:{playTime.Seconds:D2}", $"{new SteamID(client.SteamID)}");
        }
    }

    [CommandHelper(1, "<map>")]
    [ConsoleCommand("css_map", "change map")]
    public void OnCmdChangeMap(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null) return;

        var cmdArg = command.ArgString;

        if (!CheckingForAdminAndFlag(controller, AdminFlag.Changemap)) return;

        foreach (var t in GetMapFromMaps())
        {
            var isWorkshopMap = t.Trim().StartsWith("ws:", StringComparison.OrdinalIgnoreCase);

            if (!t.Trim().Contains(cmdArg.Trim(), StringComparison.OrdinalIgnoreCase)) continue;

            var map = t.StartsWith("ws:") ? t.Split("ws:")[1] : t;
            PrintToChatAll($"{controller.PlayerName}: changing the map to {map}");
            AddTimer(3.0f, () => ChangeMap(map, isWorkshopMap));
            return;
        }

        PrintToChat(controller, "This map doesn't exist");
    }

    [CommandHelper(1, "<#userid or username>")]
    [ConsoleCommand("css_slay", "kill a player")]
    public void OnCmdSlay(CCSPlayerController? controller, CommandInfo command)
    {
        var cmdArg = command.ArgString;

        if (!CheckingForAdminAndFlag(controller, AdminFlag.Slay)) return;

        ExtractValueInQuotes(cmdArg);
        var target = GetPlayerFromUserIdOrName(command.GetArg(1));

        if (!IsPlayerValid(controller, target, true)) return;

        if (target.PlayerPawn.Value != null) target.PlayerPawn.Value.CommitSuicide(true, true);

        if (controller == null)
            PrintToChatAll($"Console: Player '{target.PlayerName}' has been killed");

        ReplyToCommand(controller,
            $"{(controller != null ? controller.PlayerName : _config.BanFromConsoleName)}: Player '{target.PlayerName}' has been killed");
    }

    [CommandHelper(1, "<#userid or username>")]
    [ConsoleCommand("css_kick", "Kick a player")]
    public void OnCmdKick(CCSPlayerController? controller, CommandInfo command)
    {
        var cmdArg = command.ArgString;

        if (!CheckingForAdminAndFlag(controller, AdminFlag.Kick)) return;

        var target = GetPlayerFromUserIdOrName(ExtractValueInQuotes(cmdArg));

        if (!IsPlayerValid(controller, target)) return;

        if (controller != null)
            if (IsAdmin(target.Index) && !PlayerImmunityComparison(controller, target))
            {
                ReplyToCommand(controller, "You can't ban this player");
                return;
            }

        target.Kick("Test");

        var msg =
            $"{(controller != null ? controller.PlayerName : _config.BanFromConsoleName)}: Player '{target.PlayerName}' kicked by admin";
        ReplyToCommand(controller, msg);
    }

    [CommandHelper(2, "<#userid or username> <time_seconds> [reason]")]
    [ConsoleCommand("css_ban", "ban")]
    public void OnCmdBan(CCSPlayerController? controller, CommandInfo command)
    {
        var cmdArg = command.ArgString;

        if (!CheckingForAdminAndFlag(controller, AdminFlag.Ban)) return;

        var target = GetPlayerFromUserIdOrName(command.GetArg(1));

        if (!IsPlayerValid(controller, target)) return;

        if (controller != null)
            if (IsAdmin(target.Index) && !PlayerImmunityComparison(controller, target))
            {
                ReplyToCommand(controller, "You can't ban this player");
                return;
            }

        var endBanTime = Convert.ToInt32(command.GetArg(2));

        var startBanTimeUnix = DateTime.UtcNow.GetUnixEpoch();
        var endBanTimeUnix = DateTime.UtcNow.AddSeconds(endBanTime).GetUnixEpoch();

        var reason = "none";
        if (command.ArgCount > 3)
            reason = command.GetArg(3);

        Server.NextFrame(() =>
        {
            var msg = Database.AddBan(new User
            {
                admin_username = controller != null ? controller.PlayerName : _config.BanFromConsoleName,
                admin_steamid = controller != null ? new SteamID(controller.SteamID).SteamId2 : _config.BanFromConsoleName,
                username = target.PlayerName,
                steamid64 = target.SteamID,
                steamid = new SteamID(target.SteamID).SteamId2,
                reason = reason,
                unban_reason = "",
                admin_unlocked_username = "",
                admin_unlocked_steamid = "",
                start_ban_time = startBanTimeUnix,
                end_ban_time = endBanTime == 0 ? 0 : endBanTimeUnix,
                ban_active = true
            }).Result;
            //KickClient($"{target.UserId}");
            target.Kick("Ban");

            ReplyToCommand(controller, msg);
        });
    }

    [CommandHelper(2, "<#userid or username> <time_seconds> [reason]")]
    [ConsoleCommand("css_mute", "mute")]
    public void OnCmdMute(CCSPlayerController? controller, CommandInfo command)
    {
        var cmdArg = command.ArgString;

        if (!CheckingForAdminAndFlag(controller, AdminFlag.Generic)) return;

        var target = GetPlayerFromUserIdOrName(command.GetArg(1));

        if (!IsPlayerValid(controller, target)) return;

        if (controller != null)
            if (IsAdmin(target.Index) && !PlayerImmunityComparison(controller, target))
            {
                ReplyToCommand(controller, "You can't ban this player");
                return;
            }

        var endMuteTime = Convert.ToInt32(command.GetArg(2));

        var startMuteTimeUnix = DateTime.UtcNow.GetUnixEpoch();
        var endMuteTimeUnix = DateTime.UtcNow.AddSeconds(endMuteTime).GetUnixEpoch();

        if (target.VoiceFlags.HasFlag(VoiceFlags.Muted))
        {
            ReplyToCommand(controller, $"Player \x02'{target.PlayerName}'\x01 has already had his microphone cut off");
            return;
        }

        var reason = "none";
        if (command.ArgCount > 3)
            reason = command.GetArg(3);

        Server.NextFrame(() =>
        {
            Database.AddMute(new MuteUser
            {
                mute_type = (int)MuteType.Micro,
                admin_username = controller != null ? controller.PlayerName : _config.BanFromConsoleName,
                admin_steamid = controller != null ? new SteamID(controller.SteamID).SteamId2 : _config.BanFromConsoleName,
                username = target.PlayerName,
                steamid64 = target.SteamID,
                steamid = new SteamID(target.SteamID).SteamId2,
                reason = reason,
                unmute_reason = "",
                admin_unlocked_username = "",
                admin_unlocked_steamid = "",
                start_mute_time = startMuteTimeUnix,
                end_mute_time = endMuteTime == 0 ? 0 : endMuteTimeUnix,
                mute_active = true
            }, controller == null ? null : controller);
        });
        target.VoiceFlags = VoiceFlags.Muted;

        UpdateUserMuteLocal(target, endMuteTime, endMuteTimeUnix, (int)MuteType.Micro);
    }

    [CommandHelper(1, "<steamid | #userid> [reason]")]
    [ConsoleCommand("css_unmute", "unmute")]
    public void OnCmdUnmute(CCSPlayerController? controller, CommandInfo command)
    {
        var cmdArg = command.ArgString;

        if (!CheckingForAdminAndFlag(controller, AdminFlag.Unban)) return;

        var arg1 = command.GetArg(1);
        var steamId = arg1;
        CCSPlayerController? player = null;

        if (arg1.StartsWith('#'))
        {
            player = GetPlayerFromUserIdOrName(arg1);
        }

        var reason = "none";
        if (command.ArgCount > 2)
            reason = command.GetArg(2);

        Server.NextFrame(() =>
        {
            var msg = Database.UnmuteUser((int)MuteType.Micro,
                controller != null ? controller.PlayerName : _config.BanFromConsoleName,
                controller != null ? new SteamID(controller.SteamID).SteamId2 : _config.BanFromConsoleName,
                player == null ? steamId : new SteamID(player.SteamID).SteamId2, reason).Result;

            ReplyToCommand(controller, msg);
        });

        //var player = Utilities.GetPlayers().FirstOrDefault(u => new SteamID(u.SteamID).SteamId2 == steamId);
        if (player != null)
        {
            UpdateUserMuteLocal(player, isAdded: false);
            player.VoiceFlags = VoiceFlags.Normal;
        }
    }

    [CommandHelper(2, "<#userid or username> <time_seconds> [reason]")]
    [ConsoleCommand("css_gag", "mute")]
    public void OnCmdGag(CCSPlayerController? controller, CommandInfo command)
    {
        var cmdArg = command.ArgString;

        if (!CheckingForAdminAndFlag(controller, AdminFlag.Generic)) return;

        var target = GetPlayerFromUserIdOrName(command.GetArg(1));

        if (!IsPlayerValid(controller, target)) return;

        if (target == null)
        {
            ReplyToCommand(controller, "Player not found");
            return;
        }

        if (controller != null)
            if (IsAdmin(target.Index) && !PlayerImmunityComparison(controller, target))
            {
                ReplyToCommand(controller, "You can't ban this player");
                return;
            }

        var endMuteTime = Convert.ToInt32(command.GetArg(2));

        var startMuteTimeUnix = DateTime.UtcNow.GetUnixEpoch();
        var endMuteTimeUnix = DateTime.UtcNow.AddSeconds(endMuteTime).GetUnixEpoch();

        var reason = "none";
        if (command.ArgCount > 3)
            reason = command.GetArg(3);

        Server.NextFrame(() =>
        {
            Database.AddMute(new MuteUser
            {
                mute_type = (int)MuteType.Chat,
                admin_username = controller != null ? controller.PlayerName : _config.BanFromConsoleName,
                admin_steamid = controller != null ? new SteamID(controller.SteamID).SteamId2 : _config.BanFromConsoleName,
                username = target.PlayerName,
                steamid64 = target.SteamID,
                steamid = new SteamID(target.SteamID).SteamId2,
                reason = reason,
                unmute_reason = "",
                admin_unlocked_username = "",
                admin_unlocked_steamid = "",
                start_mute_time = startMuteTimeUnix,
                end_mute_time = endMuteTime == 0 ? 0 : endMuteTimeUnix,
                mute_active = true
            }, controller == null ? null : controller);
        });

        UpdateUserMuteLocal(target, endMuteTime, endMuteTimeUnix, (int)MuteType.Chat);
    }

    [CommandHelper(1, "<steamid | #userid> [reason]")]
    [ConsoleCommand("css_ungag", "ungag")]
    public void OnCmdUngag(CCSPlayerController? controller, CommandInfo command)
    {
        var cmdArg = command.ArgString;

        if (!CheckingForAdminAndFlag(controller, AdminFlag.Unban)) return;

        var arg1 = command.GetArg(1);
        var steamId = arg1;
        CCSPlayerController? player = null;

        if (arg1.StartsWith('#'))
        {
            player = GetPlayerFromUserIdOrName(arg1);
        }

        var reason = "none";
        if (command.ArgCount > 2)
            reason = command.GetArg(2);

        Server.NextFrame(() =>
        {
            var msg = Database.UnmuteUser((int)MuteType.Chat,
                controller != null ? controller.PlayerName : _config.BanFromConsoleName,
                controller != null ? new SteamID(controller.SteamID).SteamId2 : _config.BanFromConsoleName,
                player == null ? steamId : new SteamID(player.SteamID).SteamId2, reason).Result;

            ReplyToCommand(controller, msg);
        });

        if (player != null)
            UpdateUserMuteLocal(player, isAdded: false);
    }

    public void UpdateUserMuteLocal(CCSPlayerController? player = null, int endTime = -1, int endTimeUnix = -1,
        int type = -1,
        bool isAdded = true)
    {
        if (player != null)
        {
            var entityIndex = player.Index;
            var user = _muteUsers[entityIndex];

            if (isAdded)
            {
                _muteUsers[entityIndex] = new MuteUserLocal
                {
                    MuteType = user == null ? type : (int)MuteType.All,
                    SteamId = new SteamID(player.SteamID).SteamId2,
                    EndMuteTime = endTime == 0 ? 0 : endTimeUnix,
                    MuteActive = true
                };
            }
            else _muteUsers[entityIndex] = null;
        }
    }
    
    [CommandHelper(1, "<steamid> [username] [immunity] [flags] [time_seconds]\nif you don't want to update something, don't leave it blank, write `-` or `-s`\nExample of updating flags: css_updateadmin \"STEAM_0:0:123456\" -s -s bcd -s", CommandUsage.SERVER_ONLY)]
    [ConsoleCommand("css_updateadmin")]
    public void OnCmdUpdateAdmin(CCSPlayerController? controller, CommandInfo command)
    {
        Database.UpdateAdmin(new Admins
        {
            username = command.GetArg(2),
            steamid = command.GetArg(1),
            start_time = -1,
            end_time = int.TryParse(command.GetArg(5), out var time) ? time : -1,
            immunity = int.TryParse(command.GetArg(3), out var i) ? i : -1,
            flags = command.GetArg(4)
        });
    }

    [CommandHelper(5, "<username> <steamid> <immunity> <flags> <time_seconds>", CommandUsage.SERVER_ONLY)]
    [ConsoleCommand("css_addadmin")]
    public void OnCmdAddAdmin(CCSPlayerController? controller, CommandInfo command)
    {
        var username = command.GetArg(1);
        var steamId = command.GetArg(2);
        var immunity = command.GetArg(3);
        var flags = command.GetArg(4);

        var time = int.TryParse(command.GetArg(5), out var endTime) ? endTime : 3600;
        var startTimeUnix = DateTime.UtcNow.GetUnixEpoch();
        var endTimeUnix = DateTime.UtcNow.AddSeconds(time).GetUnixEpoch();
        
        Server.NextFrame(() =>
        {
            Database.AddAdmin(new Admins
            {
                username = username,
                steamid = steamId,
                start_time = startTimeUnix,
                end_time = time == 0 ? 0 : endTimeUnix,
                immunity = int.TryParse(immunity, out var i) ? i : 10,
                flags = flags
            }, controller);
        });
    }

    [CommandHelper(1, "<steamid> [reason]")]
    [ConsoleCommand("css_unban", "unban")]
    public void OnCmdUnban(CCSPlayerController? controller, CommandInfo command)
    {
        if (!CheckingForAdminAndFlag(controller, AdminFlag.Unban)) return;

        var steamId = command.GetArg(1);

        var reason = "none";
        if (command.ArgCount > 2)
            reason = command.GetArg(2);

        Server.NextFrame(() =>
        {
            var msg = Database.UnbanUser(
                controller != null ? controller.PlayerName : _config.BanFromConsoleName,
                controller != null ? new SteamID(controller.SteamID).SteamId2 : _config.BanFromConsoleName,
                steamId, reason).Result;

            ReplyToCommand(controller, msg);
        });
    }

    [CommandHelper(1, "<steamid>", CommandUsage.SERVER_ONLY)]
    [ConsoleCommand("css_deleteadmin", "delete admin")]
    public void OnCmdDeleteAdmin(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller != null) return;

        var cmdArg = command.ArgString;

        var steamId = ExtractValueInQuotes(cmdArg);

        Task.Run(() => Database.DeleteAdminAsync(steamId));
    }
    
    [CommandHelper(2, "<#userid or username> <time_seconds> [reason]")]
    [ConsoleCommand("css_silence")]
    public void OnCmdSilence(CCSPlayerController? controller, CommandInfo command)
    {
        if (!CheckingForAdminAndFlag(controller, AdminFlag.Generic)) return;

        var target = GetPlayerFromUserIdOrName(command.GetArg(1));

        if (!IsPlayerValid(controller, target)) return;

        if (controller != null)
            if (IsAdmin(target.Index) && !PlayerImmunityComparison(controller, target))
            {
                ReplyToCommand(controller, "You can't ban this player");
                return;
            }

        var endMuteTime = Convert.ToInt32(command.GetArg(2));

        var startMuteTimeUnix = DateTime.UtcNow.GetUnixEpoch();
        var endMuteTimeUnix = DateTime.UtcNow.AddSeconds(endMuteTime).GetUnixEpoch();

        if (target.VoiceFlags.HasFlag(VoiceFlags.All))
        {
            ReplyToCommand(controller, $"Player \x02'{target.PlayerName}'\x01 has already had all chat disabled");
            return;
        }

        var reason = "none";
        if (command.ArgCount > 3)
            reason = command.GetArg(3);

        Server.NextFrame(() =>
        {
            Database.AddMute(new MuteUser
            {
                mute_type = (int)MuteType.All,
                admin_username = controller != null ? controller.PlayerName : _config.BanFromConsoleName,
                admin_steamid = controller != null ? new SteamID(controller.SteamID).SteamId2 : _config.BanFromConsoleName,
                username = target.PlayerName,
                steamid64 = target.SteamID,
                steamid = new SteamID(target.SteamID).SteamId2,
                reason = reason,
                unmute_reason = "",
                admin_unlocked_username = "",
                admin_unlocked_steamid = "",
                start_mute_time = startMuteTimeUnix,
                end_mute_time = endMuteTime == 0 ? 0 : endMuteTimeUnix,
                mute_active = true
            }, controller);
        });
        target.VoiceFlags = VoiceFlags.Muted;

        UpdateUserMuteLocal(target, endMuteTime, endMuteTimeUnix, (int)MuteType.All);
    }
    
    [CommandHelper(1, "<#userid or steamid> [reason]")]
    [ConsoleCommand("css_unsilence", "unmute")]
    public void OnCmdUnSilence(CCSPlayerController? controller, CommandInfo command)
    {
        if (!CheckingForAdminAndFlag(controller, AdminFlag.Unban)) return;

        var arg1 = command.GetArg(1);
        var steamId = arg1;
        CCSPlayerController? player = null;

        if (arg1.StartsWith('#'))
        {
            player = GetPlayerFromUserIdOrName(arg1);
        }

        var reason = "none";
        if (command.ArgCount > 2)
            reason = command.GetArg(2);

        Server.NextFrame(() =>
        {
            var msg = Database.UnmuteUser((int)MuteType.All,
                controller != null ? controller.PlayerName : _config.BanFromConsoleName,
                controller != null ? new SteamID(controller.SteamID).SteamId2 : _config.BanFromConsoleName,
                player == null ? steamId : new SteamID(player.SteamID).SteamId2, reason).Result;

            ReplyToCommand(controller, msg);
        });

        //var player = Utilities.GetPlayers().FirstOrDefault(u => new SteamID(u.SteamID).SteamId2 == steamId);
        if (player != null)
        {
            UpdateUserMuteLocal(player, isAdded: false);
            player.VoiceFlags = VoiceFlags.Normal;
        }
    }

    private CCSPlayerController? GetPlayerFromUserIdOrName(string player)
    {
        if (player.StartsWith('#') && int.TryParse(player.Trim('#'), out var index))
            return Utilities.GetPlayerFromUserid(index);

        return Utilities.GetPlayers().FirstOrDefault(u => u.PlayerName.Contains(player));
    }

    public bool CheckingForAdminAndFlag(CCSPlayerController? controller, AdminFlag flag)
    {
        if (controller == null) return true;
        var entityIndex = controller.Index;

        if (IsAdmin(entityIndex) && HasLetterInUserFlags(entityIndex, (char)flag)) return true;

        PrintToChat(controller, "you do not have access to this command");
        return false;
    }

    private string GetTextInsideQuotes(string input)
    {
        var startIndex = input.IndexOf('"');
        var endIndex = input.LastIndexOf('"');

        if (startIndex != -1 && endIndex != -1 && startIndex < endIndex)
        {
            return input.Substring(startIndex + 1, endIndex - startIndex - 1);
        }

        return string.Empty;
    }

    private string[] ParseCommandArguments(string argString)
    {
        var parse = Regex.Matches(argString, @"[\""].+?[\""]|[^ ]+")
            .Select(m => m.Value.Trim('"'))
            .ToArray();

        return parse;
    }

    private bool IsPlayerValid(CCSPlayerController? player, CCSPlayerController? target, bool isKickOrSlap = false)
    {
        if (target == null || !target.IsValid)
        {
            ReplyToCommand(player, "Player not found");
            return false;
        }

        if (isKickOrSlap)
        {
            if (!target.PawnIsAlive)
            {
                ReplyToCommand(player, "The player is invalid");
                return false;
            }
        }

        return true;
    }

    private string BuildConnectionString()
    {
        var dbConfig = _config.Connection;

        Console.WriteLine("Building connection string");
        var builder = new MySqlConnectionStringBuilder
        {
            Database = dbConfig.Database,
            UserID = dbConfig.User,
            Password = dbConfig.Password,
            Server = dbConfig.Host,
            Port = (uint)dbConfig.Port,
        };

        Console.WriteLine("OK!");
        return builder.ConnectionString;
    }

    private static CCSPlayerController? GetPlayerFromSteamId(string steamId)
    {
        return Utilities.GetPlayers().FirstOrDefault(u =>
            u.AuthorizedSteamID != null &&
            (u.AuthorizedSteamID.SteamId2.ToString().Equals(steamId, StringComparison.OrdinalIgnoreCase) ||
             u.AuthorizedSteamID.SteamId64.ToString().Equals(steamId, StringComparison.OrdinalIgnoreCase)));
    }

    private Config LoadConfig()
    {
        var configPath = Path.Combine(ModuleDirectory, "database.json");
        if (!File.Exists(configPath)) return CreateConfig(configPath);

        var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath))!;

        return config;
    }

    private Config CreateConfig(string configPath)
    {
        var config = new Config
        {
            BanFromConsoleName = "Console",
            Connection = new BaseAdminDb
            {
                Host = "",
                Database = "",
                User = "",
                Password = "",
                Port = 3306
            }
        };

        var mapsConfig = Path.Combine(ModuleDirectory, "maps.txt");
        if (!File.Exists(mapsConfig))
            File.WriteAllLines(mapsConfig, new[] { "de_dust2", "ws:awp_lego_2" });

        File.WriteAllText(configPath,
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        return config;
    }

    public IEnumerable<string> ReadAllBanReasons()
    {
        var path = Path.Combine(ModuleDirectory, "admin/ban_reasons.txt");

        if (!File.Exists(path))
            File.WriteAllLines(path, new[] { "Aim", "Wallhack", "Violation" });

        return File.ReadAllLines(path);
    }

    public IEnumerable<string> ReadAllMuteReasons()
    {
        var path = Path.Combine(ModuleDirectory, "admin/mute_reasons.txt");

        if (!File.Exists(path))
            File.WriteAllLines(path, new[] { "Spam Mic/Chat", "Inappropriate behavior", "16+" });

        return File.ReadAllLines(path);
    }

    public IEnumerable<string> ReadAllTime()
    {
        var path = Path.Combine(ModuleDirectory, "admin/times.txt");

        if (!File.Exists(path))
            File.WriteAllLines(path, new[] { "permanently:0", "1 hours:3600", "1 day:86400", "1 week:604800" });

        return File.ReadAllLines(path);
    }

    public string[] GetMapFromMaps()
    {
        var mapText = File.ReadAllText(Path.Combine(ModuleDirectory, "maps.txt"));
        var mapList = mapText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).ToArray();

        return mapList;
    }

    public IEnumerable<string> ReadAllImmunity()
    {
        var path = Path.Combine(ModuleDirectory, "admin/immunity.txt");

        if (!File.Exists(path))
            File.WriteAllLines(path, new[] { "1", "10", "20", "30" });

        return File.ReadAllLines(path);
    }

    public bool HasLetterInUserFlags(uint index, char letter)
    {
        if (_adminUsers[index] == null) return false;

        var adminUser = _adminUsers[index]!.flags;

        if (adminUser.Contains((char)AdminFlag.Root)) return true;

        return !string.IsNullOrEmpty(adminUser) && adminUser.Contains(letter);
    }

    private bool PlayerImmunityComparison(CCSPlayerController player, CCSPlayerController target)
    {
        if (_adminUsers[target.Index] == null) return false;

        return _adminUsers[player.Index]!.immunity >= _adminUsers[target.Index]!.immunity;
    }

    public bool IsAdmin(uint index)
    {
        var user = _adminUsers[index];
        if (user == null) return false;

        if (user.end_time != 0 && DateTime.UtcNow.GetUnixEpoch() > user.end_time) return false;

        return user.end_time == 0 || DateTime.UtcNow.GetUnixEpoch() < user.end_time;
    }

    private bool IsUserMute(uint index)
    {
        var user = _muteUsers[index];
        if (user == null) return false;

        if (DateTime.UtcNow.GetUnixEpoch() > user.EndMuteTime) return false;

        return user.EndMuteTime == 0 || DateTime.UtcNow.GetUnixEpoch() < user.EndMuteTime;
    }

    public string ExtractValueInQuotes(string input)
    {
        var match = Regex.Match(input, @"""([^""]*)""");

        return match.Success ? match.Groups[1].Value : input;
    }

    public void ReplyToCommand(CCSPlayerController? controller, string message, params object?[] args)
    {
        if (controller != null)
            PrintToChat(controller, message);
        else
            PrintLogInfo(message, args);
    }

    public void ChangeMap(string mapName, bool isWorkshop = false)
    {
        Server.ExecuteCommand($"{(isWorkshop ? "ds_workshop_changelevel" : "map")} {mapName}");
    }

    public void PrintToChat(CCSPlayerController controller, string msg)
    {
        controller.PrintToChat($"[ {ChatColors.Blue}Admin \x01] {msg}");
    }

    public void PrintToCenterAll(string msg)
    {
        VirtualFunctions.ClientPrintAll(HudDestination.Center, $"Admin: {msg}", 0, 0, 0, 0);
    }

    public void PrintToChatAll(string msg)
    {
        Server.PrintToChatAll($"[ {ChatColors.Blue}Admin \x01] {msg}");
    }

    private void PrintToConsole(CCSPlayerController client, string msg)
    {
        VirtualFunctions.ClientPrint(client.Handle, HudDestination.Console, msg, 0, 0, 0, 0);
    }

    public void PrintLogInfo(string? message, params object?[] args)
    {
        Logger.LogInformation($"{message}", args);
    }

    public void PrintLogError(string? message, params object?[] args)
    {
        Logger.LogError($"{message}", args);
    }
}

public class Config
{
    public string BanFromConsoleName { get; init; } = "Console";
    public BaseAdminDb Connection { get; init; } = null!;
}

public class BaseAdminDb
{
    public required string Host { get; init; }
    public required string Database { get; init; }
    public required string User { get; init; }
    public required string Password { get; init; }
    public int Port { get; init; }
}

public class AdminData
{
    public ulong SteamId { get; set; }
    public bool TagEnabled { get; set; }
}