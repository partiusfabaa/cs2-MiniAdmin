using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Dapper;
using MySqlConnector;

namespace BaseAdmin;

public class BaseAdmin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "Mini Admin";
    public override string ModuleVersion => "v1.0.4";

    private static string _dbConnectionString = string.Empty;
    private readonly DateTime[] _playerPlayTime = new DateTime[Server.MaxPlayers];

    //MENU
    public readonly string Prefix = "[\x0C Admin Menu \x01]";

    private readonly MuteUserLocal?[] _muteUsers = new MuteUserLocal[65];
    private readonly Admins?[] _adminUsers = new Admins[65];

    public Database Database = null!;
    private AdminChat _adminChat = null!;

    public enum MuteType
    {
        Micro = 0,
        Chat,
        All
    }

    public enum AdminFlag
    {
        Ban = 'a',
        Unban = 'b',
        Mute = 'c',
        AdminChat = 'd',
        Slay = 'e',
        Kick = 'f',
        Map = 'g',
        Rcon = 'h',
        Root = 'z'
    }

    public override void Load(bool hotReload)
    {
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
            _playerPlayTime[slot + 1] = DateTime.MinValue;
        });

        AddCommandListener("say", CommandListener_Say);
        AddCommandListener("say_team", CommandListener_SayTeam);

        AddTimer(300, () => Task.Run(Database.DeleteExpiredAdminsAsync), TimerFlags.REPEAT);
    }

    private HookResult ProcessChatMessage(CCSPlayerController? controller, CommandInfo info, bool sendToAllAdmins)
    {
        if (controller == null) return HookResult.Continue;

        var msg = GetTextInsideQuotes(info.ArgString);
        var entityIndex = controller.Index;

        if (msg.StartsWith('@'))
        {
            if (CheckingForAdminAndFlag(controller, AdminFlag.AdminChat))
            {
                if (sendToAllAdmins)
                {
                    _adminChat.SendToAllFromAdmin(msg.Trim('@'));
                    return HookResult.Handled;
                }

                foreach (var player in Utilities.GetPlayers())
                {
                    if (!CheckingForAdminAndFlag(controller, AdminFlag.AdminChat)) continue;

                    _adminChat.SendToAdminChat(player,
                        $" {ChatColors.Blue}{controller.PlayerName}{ChatColors.Default}: {msg.Trim('@')}");
                    return HookResult.Handled;
                }
            }
            else
            {
                if (!IsUserMute(entityIndex))
                {
                    foreach (var player in Utilities.GetPlayers())
                    {
                        if (!CheckingForAdminAndFlag(controller, AdminFlag.AdminChat)) continue;

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
        var userAdmin = await Database.IsUserAdmin(steamId.SteamId2);

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

            Server.NextFrame(() => Utilities.GetPlayerFromSlot(playerSlot).Clan = "[Admin]");
        }

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
        }
    }

    private async Task OnClientConnectedAsync(int slot, CCSPlayerController player, SteamID steamId)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            var unbanUsers = await connection.QueryAsync<User>(
                "SELECT * FROM miniadmin_bans WHERE end_ban_time <= @CurrentTime AND ban_active = 1 AND end_ban_time != 0",
                new { CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });

            foreach (var user in unbanUsers)
            {
                PrintToServer($"Unban: {user.steamid}", ConsoleColor.DarkMagenta);
                await Database.UnbanUser("Console", "Console", user.steamid, "The deadline has passed");
            }

            var unmuteUsers = await connection.QueryAsync<User>(
                "SELECT * FROM miniadmin_mute WHERE end_mute_time <= @CurrentTime AND mute_active = 1 AND end_mute_time != 0",
                new { CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });

            foreach (var user in unmuteUsers)
            {
                PrintToServer($"Unmute: {user.steamid}", ConsoleColor.DarkMagenta);
                await Database.UnmuteUser(-1, "Console", "Console", user.steamid, "The deadline has passed");
                Server.NextFrame(() => player.VoiceFlags = VoiceFlags.Normal);
            }

            await Database.DeleteExpiredAdminsAsync();

            var banUser = await connection.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM miniadmin_bans WHERE steamid64 = @SteamId64 AND ban_active = 1",
                new { steamId.SteamId64 });

            if (banUser != null)
                Server.NextFrame(() => Server.ExecuteCommand($"kickid {player.UserId}"));
            else
                _playerPlayTime[slot + 1] = DateTime.Now;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    [CommandHelper(0, "<#userid or username>", CommandUsage.CLIENT_ONLY)]
    [ConsoleCommand("css_noclip")]
    public void OnCmdNoclip(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null || !CheckingForAdminAndFlag(controller, AdminFlag.Root)) return;

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

    [CommandHelper(1, "<command>", CommandUsage.CLIENT_ONLY)]
    [ConsoleCommand("css_rcon")]
    public void OnCmdRcon(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null || !CheckingForAdminAndFlag(controller, AdminFlag.Rcon)) return;

        Server.ExecuteCommand(command.ArgString);
    }

    [CommandHelper(1, "<#userid or username> <damage>", CommandUsage.CLIENT_ONLY)]
    [ConsoleCommand("css_slap")]
    public void OnCmdSlap(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null || !CheckingForAdminAndFlag(controller, AdminFlag.Slay)) return;
        
        var player = GetPlayerFromUserIdOrName(command.GetArg(1));
        
        var damage = 0;
        if (command.ArgCount >= 2)
            damage = int.Parse(command.GetArg(2));
        
        if (!IsPlayerValid(controller, player)) return;

        var playerPawn = player.PlayerPawn.Value;

        if (playerPawn == null) return;

        playerPawn.AbsVelocity.X += Random.Shared.Next(100, 200);
        playerPawn.AbsVelocity.Y += Random.Shared.Next(100, 200);
        playerPawn.AbsVelocity.Z += Random.Shared.Next(200, 300);
        playerPawn.Health -= damage;

        if (playerPawn.Health <= 0) playerPawn.CommitSuicide(true, true);
    }

    [ConsoleCommand("css_who")]
    public void OnCmdWho(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller != null) return;

        var maxNameLength = 0;

        var id = 0;
        foreach (var client in Utilities.GetPlayers())
        {
            var playerName = !string.IsNullOrWhiteSpace(client.PlayerName) ? client.PlayerName : "unknown";
            var playerNameLength = playerName.Length;
            maxNameLength = Math.Max(maxNameLength, playerNameLength);

            var adminStatus = IsAdmin(client.Index) ? "admin " : "player";

            var index = client.Index;
            var playTime = DateTime.Now - _playerPlayTime[index];

            id ++;
            var formattedOutput =
                $"{id,-1} - {playerName,-15} | {adminStatus,-6} | Playtime: {playTime.Hours:D2}:{playTime.Minutes:D2}:{playTime.Seconds:D2} | {new SteamID(client.SteamID)}";

            PrintToServer(formattedOutput, ConsoleColor.Magenta);
        }
    }

    [CommandHelper(1, "<map>")]
    [ConsoleCommand("css_map", "change map")]
    public void OnCmdChangeMap(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null) return;

        var cmdArg = command.ArgString;

        if (!CheckingForAdminAndFlag(controller, AdminFlag.Map)) return;

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

        if (!IsPlayerValid(controller, target)) return;

        if (target.PlayerPawn.Value != null) target.PlayerPawn.Value.CommitSuicide(true, true);

        if (controller == null)
            PrintToChatAll($"Console: Player '{target.PlayerName}' has been killed");

        ReplyToCommand(controller,
            $"{(controller != null ? controller.PlayerName : "Console")}: Player '{target.PlayerName}' has been killed");
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
            if (PlayerImmunityComparison(controller, target))
            {
                ReplyToCommand(controller, "You can't ban this player");
                return;
            }

        KickClient($"{target.UserId}");

        var msg =
            $"{(controller != null ? controller.PlayerName : "Console")}: Player '{target.PlayerName}' kicked by admin";
        ReplyToCommand(controller, msg);
    }

    [CommandHelper(3, "<#userid or username> <time_seconds> <reason>")]
    [ConsoleCommand("css_ban", "ban")]
    public void OnCmdBan(CCSPlayerController? controller, CommandInfo command)
    {
        var cmdArg = command.ArgString;

        if (!CheckingForAdminAndFlag(controller, AdminFlag.Ban)) return;

        var splitCmdArgs = ParseCommandArguments(cmdArg);

        var target = GetPlayerFromUserIdOrName(ExtractValueInQuotes(splitCmdArgs[0]));

        if (!IsPlayerValid(controller, target)) return;

        if (controller != null)
            if (PlayerImmunityComparison(controller, target))
            {
                ReplyToCommand(controller, "You can't ban this player");
                return;
            }

        var endBanTime = Convert.ToInt32(ExtractValueInQuotes(splitCmdArgs[1]));

        var startBanTimeUnix = DateTime.UtcNow.GetUnixEpoch();
        var endBanTimeUnix = DateTime.UtcNow.AddSeconds(endBanTime).GetUnixEpoch();

        Server.NextFrame(() =>
        {
            var msg = Database.AddBan(new User
            {
                admin_username = controller != null ? controller.PlayerName : "Console",
                admin_steamid = controller != null ? new SteamID(controller.SteamID).SteamId2 : "Console",
                username = target.PlayerName,
                steamid64 = target.SteamID,
                steamid = new SteamID(target.SteamID).SteamId2,
                reason = ExtractValueInQuotes(splitCmdArgs[2]),
                unban_reason = "",
                admin_unlocked_username = "",
                admin_unlocked_steamid = "",
                start_ban_time = startBanTimeUnix,
                end_ban_time = endBanTime == 0 ? 0 : endBanTimeUnix,
                ban_active = true
            }).Result;
            KickClient($"{target.UserId}");

            ReplyToCommand(controller, msg);
        });
    }

    [CommandHelper(3, "<#userid or username> <time_seconds> <reason>")]
    [ConsoleCommand("css_mute", "mute")]
    public void OnCmdMute(CCSPlayerController? controller, CommandInfo command)
    {
        var cmdArg = command.ArgString;

        if (!CheckingForAdminAndFlag(controller, AdminFlag.Mute)) return;

        var splitCmdArgs = ParseCommandArguments(cmdArg);

        var target = GetPlayerFromUserIdOrName(ExtractValueInQuotes(splitCmdArgs[0]));

        if (!IsPlayerValid(controller, target)) return;

        if (controller != null)
            if (PlayerImmunityComparison(controller, target))
            {
                ReplyToCommand(controller, "You can't ban this player");
                return;
            }

        var endMuteTime = Convert.ToInt32(ExtractValueInQuotes(splitCmdArgs[1]));

        var startMuteTimeUnix = DateTime.UtcNow.GetUnixEpoch();
        var endMuteTimeUnix = DateTime.UtcNow.AddSeconds(endMuteTime).GetUnixEpoch();

        if (target.VoiceFlags.HasFlag(VoiceFlags.Muted))
        {
            PrintToChat(controller, $"Player \x02'{target.PlayerName}'\x08 has already had his microphone cut off");
            return;
        }

        Server.NextFrame(() =>
        {
            Database.AddMute(new MuteUser
            {
                mute_type = (int)MuteType.Micro,
                admin_username = controller != null ? controller.PlayerName : "Console",
                admin_steamid = controller != null ? new SteamID(controller.SteamID).SteamId2 : "Console",
                username = target.PlayerName,
                steamid64 = target.SteamID,
                steamid = new SteamID(target.SteamID).SteamId2,
                reason = ExtractValueInQuotes(splitCmdArgs[2]),
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

    [CommandHelper(2, "<steamid> <reason>")]
    [ConsoleCommand("css_unmute", "unmute")]
    public void OnCmdUnmute(CCSPlayerController? controller, CommandInfo command)
    {
        var cmdArg = command.ArgString;

        if (!CheckingForAdminAndFlag(controller, AdminFlag.Unban)) return;

        var splitCmdArgs = ParseCommandArguments(cmdArg);

        var steamId = ExtractValueInQuotes(splitCmdArgs[0]);

        Server.NextFrame(() =>
        {
            var msg = Database.UnmuteUser((int)MuteType.Micro,
                controller != null ? controller.PlayerName : "Console",
                controller != null ? new SteamID(controller.SteamID).SteamId2 : "Console",
                steamId, ExtractValueInQuotes(splitCmdArgs[1])).Result;

            ReplyToCommand(controller, msg);
        });

        var player = Utilities.GetPlayers().FirstOrDefault(u => new SteamID(u.SteamID).SteamId2 == steamId);
        if (player != null)
        {
            UpdateUserMuteLocal(player, isAdded: false);
            player.VoiceFlags = VoiceFlags.Normal;
        }
    }

    [CommandHelper(3, "<#userid or username> <time_seconds> <reason>")]
    [ConsoleCommand("css_gag", "mute")]
    public void OnCmdGag(CCSPlayerController? controller, CommandInfo command)
    {
        var cmdArg = command.ArgString;

        if (!CheckingForAdminAndFlag(controller, AdminFlag.Mute)) return;

        var splitCmdArgs = ParseCommandArguments(cmdArg);

        var target = GetPlayerFromUserIdOrName(ExtractValueInQuotes(splitCmdArgs[0]));

        if (!IsPlayerValid(controller, target)) return;

        if (controller != null)
            if (PlayerImmunityComparison(controller, target))
            {
                ReplyToCommand(controller, "You can't ban this player");
                return;
            }

        var endMuteTime = Convert.ToInt32(ExtractValueInQuotes(splitCmdArgs[1]));

        var startMuteTimeUnix = DateTime.UtcNow.GetUnixEpoch();
        var endMuteTimeUnix = DateTime.UtcNow.AddSeconds(endMuteTime).GetUnixEpoch();

        Server.NextFrame(() =>
        {
            Database.AddMute(new MuteUser
            {
                mute_type = (int)MuteType.Chat,
                admin_username = controller != null ? controller.PlayerName : "Console",
                admin_steamid = controller != null ? new SteamID(controller.SteamID).SteamId2 : "Console",
                username = target.PlayerName,
                steamid64 = target.SteamID,
                steamid = new SteamID(target.SteamID).SteamId2,
                reason = ExtractValueInQuotes(splitCmdArgs[2]),
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

    [CommandHelper(2, "<steamid> <reason>")]
    [ConsoleCommand("css_ungag", "ungag")]
    public void OnCmdUngag(CCSPlayerController? controller, CommandInfo command)
    {
        var cmdArg = command.ArgString;

        if (!CheckingForAdminAndFlag(controller, AdminFlag.Unban)) return;

        var splitCmdArgs = ParseCommandArguments(cmdArg);

        var steamId = ExtractValueInQuotes(splitCmdArgs[0]);

        Server.NextFrame(() =>
        {
            var msg = Database.UnmuteUser((int)MuteType.Chat,
                controller != null ? controller.PlayerName : "Console",
                controller != null ? new SteamID(controller.SteamID).SteamId2 : "Console",
                steamId, ExtractValueInQuotes(splitCmdArgs[1])).Result;

            ReplyToCommand(controller, msg);
        });


        var player = Utilities.GetPlayers().FirstOrDefault(u => new SteamID(u.SteamID).SteamId2 == steamId);
        if (player != null)
            UpdateUserMuteLocal(player, isAdded: false);
    }

    [CommandHelper(5, "<username> <steamid> <immunity> <flags> <time_seconds>", CommandUsage.SERVER_ONLY)]
    [ConsoleCommand("css_addadmin")]
    public void OnCmdAddAdmin(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller != null) return;

        var cmdArg = command.ArgString;

        var splitCmdArgs = ParseCommandArguments(cmdArg);

        var endTime = Convert.ToInt32(ExtractValueInQuotes(splitCmdArgs[4]));
        var startTimeUnix = DateTime.UtcNow.GetUnixEpoch();
        var endTimeUnix = DateTime.UtcNow.AddSeconds(endTime).GetUnixEpoch();

        Server.NextFrame(() =>
        {
            var msg = Database.AddAdmin(new Admins
            {
                username = ExtractValueInQuotes(splitCmdArgs[0]),
                steamid = ExtractValueInQuotes(splitCmdArgs[1]),
                start_time = startTimeUnix,
                end_time = endTime == 0 ? 0 : endTimeUnix,
                immunity = int.Parse(ExtractValueInQuotes(splitCmdArgs[2])),
                flags = ExtractValueInQuotes(splitCmdArgs[3])
            }).Result;

            PrintToServer(msg, ConsoleColor.Green);
        });
    }

    [CommandHelper(2, "<steamid> <reason>")]
    [ConsoleCommand("css_unban", "unban")]
    public void OnCmdUnban(CCSPlayerController? controller, CommandInfo command)
    {
        var cmdArg = command.ArgString;

        if (!CheckingForAdminAndFlag(controller, AdminFlag.Unban)) return;

        var splitCmdArgs = ParseCommandArguments(cmdArg);

        var steamId = ExtractValueInQuotes(splitCmdArgs[0]);

        Server.NextFrame(() =>
        {
            var msg = Database.UnbanUser(
                controller != null ? controller.PlayerName : "Console",
                controller != null ? new SteamID(controller.SteamID).SteamId2 : "Console",
                steamId,
                ExtractValueInQuotes(splitCmdArgs[1])).Result;

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

    private CCSPlayerController? GetPlayerFromUserIdOrName(string player)
    {
        if (player.StartsWith('#') && int.TryParse(player.Trim('#'), out var index))
            return Utilities.GetPlayerFromUserid(index);

        return Utilities.GetPlayers().FirstOrDefault(u => u.PlayerName == player);
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

    private bool IsPlayerValid(CCSPlayerController? player, CCSPlayerController? target)
    {
        if (target == null || !target.IsValid)
        {
            ReplyToCommand(player, "Player not found");
            return false;
        }

        if (!target.PawnIsAlive)
        {
            ReplyToCommand(player, "The player is invalid");
            return false;
        }

        return true;
    }

    private string BuildConnectionString()
    {
        var dbConfig = LoadConfig();

        Console.WriteLine("Building connection string");
        var builder = new MySqlConnectionStringBuilder
        {
            Database = dbConfig.Connection.Database,
            UserID = dbConfig.Connection.User,
            Password = dbConfig.Connection.Password,
            Server = dbConfig.Connection.Host,
            Port = 3306,
        };

        Console.WriteLine("OK!");
        return builder.ConnectionString;
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
            Connection = new BaseAdminDb
            {
                Host = "",
                Database = "",
                User = "",
                Password = ""
            }
        };

        var mapsConfig = Path.Combine(ModuleDirectory, "maps.txt");
        if (!File.Exists(mapsConfig))
            File.WriteAllLines(mapsConfig, new[] { "de_dust2", "de_mirage" });

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

        return _adminUsers[player.Index]!.immunity >
               _adminUsers[target.Index]!.immunity;
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

    public void ReplyToCommand(CCSPlayerController? controller, string msg)
    {
        if (controller != null)
            PrintToChat(controller, msg);
        else
            PrintToServer(msg, ConsoleColor.DarkMagenta);
    }

    public void ChangeMap(string mapName, bool isWorkshop = false)
    {
        Server.ExecuteCommand($"{(isWorkshop ? "ds_workshop_changelevel" : "map")} {mapName}");
    }

    public void KickClient(string userId)
    {
        Server.ExecuteCommand($"kickid {userId}");
    }

    public void PrintToChat(CCSPlayerController controller, string msg)
    {
        controller.PrintToChat($"\x08[\x0C Admin \x08] {msg}");
    }

    public void PrintToChatAll(string msg)
    {
        Server.PrintToChatAll($"\x08[\x0C Admin \x08] {msg}");
    }

    private void PrintToConsole(CCSPlayerController client, string msg)
    {
        client.PrintToConsole(msg);
    }

    public void PrintToServer(string msg, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine($"[ Admin ] {msg}");
        Console.ResetColor();
    }
}

public static class GetUnixTime
{
    public static int GetUnixEpoch(this DateTime dateTime)
    {
        var unixTime = dateTime.ToUniversalTime() -
                       new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return (int)unixTime.TotalSeconds;
    }
}

public class Config
{
    public BaseAdminDb Connection { get; set; } = null!;
}

public class BaseAdminDb
{
    public required string Host { get; init; }
    public required string Database { get; init; }
    public required string User { get; init; }
    public required string Password { get; init; }
}