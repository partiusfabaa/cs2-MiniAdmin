﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseAdmin.Commands;
using BaseAdmin.Commands.Commands;
using BaseAdmin.Menu;
using BaseAdminApi;
using BaseAdminApi.Enums;
using BaseAdminApi.Models;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
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
    public override string ModuleName => "Base Admin";
    public override string ModuleVersion => "v2.0.0";

    private static string _dbConnectionString = string.Empty;

    private readonly DateTime[] _playerPlayTime = new DateTime[66];
    public readonly Dictionary<ulong, Admin> AdminUsers = new();
    public readonly Dictionary<ulong, MuteUser> MuteUsers = new();

    public Database Database = null!;
    public Config Config = new();
    public BaseConfig BaseConfig = new();
    public MenuService Menu = null!;

    private IBaseAdminApi? _api;

    public new CommandManager CommandManager = null!;
    public BaseCommands BaseCommands = null!;
    public FunCommands FunCommands = null!;

    public override void Load(bool hotReload)
    {
        _api = new BaseAdminApi(this);
        Capabilities.RegisterPluginCapability(IBaseAdminApi.Capability, () => _api);
        LoadConfig();

        Menu = new MenuService(this);

        CommandManager = new CommandManager(this);
        CommandManager.RegisterCommands(this);

        BaseCommands = new BaseCommands(this);
        CommandManager.RegisterCommands(BaseCommands);

        FunCommands = new FunCommands(this);
        CommandManager.RegisterCommands(FunCommands);

        _dbConnectionString = BuildConnectionString();
        Database = new Database(this, _dbConnectionString);

        Task.Run(Database.CreateTable);
        Task.Run(Database.CreateAdminsTable);
        Task.Run(Database.CreateMuteTable);

        RegisterListener<Listeners.OnClientAuthorized>((slot, id) =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            Task.Run(() => OnClientAuthorizedAsync(slot, player, id));
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            var player = @event.Userid;
            if (player == null || player.IsBot) return HookResult.Continue;

            AdminUsers.Remove(player.SteamID);
            MuteUsers.Remove(player.SteamID);
            _playerPlayTime[player.Slot] = DateTime.MinValue;

            return HookResult.Continue;
        });

        AddTimer(10, () => { Task.Run(Database.DeleteExpiredAdminsAsync); }, TimerFlags.REPEAT);

        AddCommandListener("say", Listener_Say);
        AddCommandListener("say_team", Listener_Say);
    }

    private HookResult Listener_Say(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null) return HookResult.Continue;
        var command = info.GetArg(0);

        if (command is "say_team")
        {
        }
        else
        {
        }

        if (IsPlayerMuted(player.SteamID, MuteType.Chat))
        {
            if (!MuteUsers.TryGetValue(player.SteamID, out var muteUser)) return HookResult.Continue;

            if (!muteUser.IsTimerActive)
            {
                var endTime = DateTimeOffset.FromUnixTimeSeconds(muteUser.end_mute_time).UtcDateTime;
                var timeEnd = endTime - DateTime.UtcNow;

                var time =
                    $"{(timeEnd.Days == 0 ? "" : $"{timeEnd.Days}d, ")}{timeEnd.Hours:00}:{timeEnd.Minutes:00}:{timeEnd.Seconds:00}";

                PrintToChat(player, Localizer["player.mute.chat_disabled", time]);
                muteUser.IsTimerActive = true;
                AddTimer(5.0f, () => muteUser.IsTimerActive = false);
            }

            return HookResult.Handled;
        }

        return HookResult.Continue;
    }

    private async Task OnClientAuthorizedAsync(int slot, CCSPlayerController player, SteamID steamId)
    {
        try
        {
            await LoadAdminUserAsync(steamId);
            await LoadMuteUserAsync(steamId);
            await using var connection = new MySqlConnection(_dbConnectionString);
            await connection.OpenAsync();

            var unbanUsers = await connection.QueryFirstOrDefaultAsync<BanUser>(
                "SELECT * FROM miniadmin_bans WHERE steamid64 = @SteamId64 AND end_ban_time <= @CurrentTime AND ban_active = 1 AND end_ban_time != 0",
                new { CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), steamId.SteamId64 });

            if (unbanUsers != null)
            {
                PrintLogInfo("Unban: {steamid}", unbanUsers.steamid);
                await Database.UnbanUser(null, Config.BanFromConsoleName, Config.BanFromConsoleName, unbanUsers.steamid,
                    "The deadline has passed");
            }

            var unmuteUsers = await connection.QueryFirstOrDefaultAsync<MuteUser>(
                "SELECT * FROM miniadmin_mute WHERE steamid64 = @SteamId64 AND end_mute_time <= @CurrentTime AND mute_active = 1 AND end_mute_time != 0",
                new { CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), steamId.SteamId64 });

            if (unmuteUsers != null)
            {
                PrintLogInfo("Unmute: {steamid}", unmuteUsers.steamid);
                await Database.UnmuteUser(null, -1, Config.BanFromConsoleName, Config.BanFromConsoleName,
                    unmuteUsers.steamid, "The deadline has passed");

                if (unmuteUsers.mute_type is (int)MuteType.All or (int)MuteType.Micro)
                    await Server.NextFrameAsync(() => player.VoiceFlags = VoiceFlags.Normal);
            }

            var banUser = await connection.QueryFirstOrDefaultAsync<BanUser>(
                "SELECT * FROM miniadmin_bans WHERE steamid64 = @SteamId64 AND ban_active = 1",
                new { steamId.SteamId64 });

            if (banUser != null)
                await Server.NextFrameAsync(() => player.Kick("Ban"));
            else
                _playerPlayTime[slot] = DateTime.Now;
        }
        catch (Exception e)
        {
            PrintLogError(e.ToString());
        }
    }

    private async Task LoadMuteUserAsync(SteamID steamId)
    {
        try
        {
            var muteUser = await Database.GetActiveMuteAsync(steamId.SteamId2);
            if (muteUser != null)
            {
                MuteUsers[steamId.SteamId64] = new MuteUser
                {
                    admin_username = muteUser.admin_username,
                    admin_steamid = muteUser.admin_steamid,
                    username = muteUser.username,
                    steamid = muteUser.steamid,
                    reason = muteUser.reason,
                    unmute_reason = muteUser.unmute_reason
                };
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task LoadAdminUserAsync(SteamID steamId)
    {
        try
        {
            var userAdmin = await Database.GetAdminFromDb(steamId.SteamId2);

            if (userAdmin != null)
            {
                AdminUsers[steamId.SteamId64] = new Admin
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
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public CsTeam GetTeam(string name)
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

    public (string name, string id) GetNameAndId(CCSPlayerController? player)
    {
        var adminName = Config.BanFromConsoleName;
        var adminSteamId = Config.BanFromConsoleName;

        if (player != null)
        {
            adminName = player.PlayerName;
            adminSteamId = new SteamID(player.SteamID).SteamId2;
        }

        return (adminName, adminSteamId);
    }

    public bool CheckingForAdminAndFlag(CCSPlayerController? controller, AdminFlag flag)
    {
        if (controller == null) return true;

        if (IsPlayerAdmin(controller.SteamID) && HasLetterInUserFlags(controller.SteamID, (char)flag)) return true;

        PrintToChat(controller, Localizer["not_have_access"]);
        return false;
    }

    public bool IsPlayerAdmin(ulong steamId)
    {
        if (!AdminUsers.TryGetValue(steamId, out var admin)) return false;

        if (admin.end_time != 0 && DateTime.UtcNow.GetUnixEpoch() > admin.end_time) return false;

        return admin.end_time == 0 || DateTime.UtcNow.GetUnixEpoch() < admin.end_time;
    }

    public bool IsPlayerMuted(ulong steamId, MuteType muteType)
    {
        if (!MuteUsers.TryGetValue(steamId, out var muteUser)) return false;
        if (!muteUser.mute_active) return false;

        if (muteUser.end_mute_time != 0 && DateTime.UtcNow.GetUnixEpoch() > muteUser.end_mute_time)
            return false;

        return (muteUser.end_mute_time == 0 || DateTime.UtcNow.GetUnixEpoch() < muteUser.end_mute_time) &&
               (muteUser.mute_type == (int)muteType || muteUser.mute_type == (int)MuteType.All);
    }

    public bool HasLetterInUserFlags(ulong steam, char letter)
    {
        if (!AdminUsers.TryGetValue(steam, out var admin)) return false;

        var flags = admin.flags;

        if (flags.Contains((char)AdminFlag.Root)) return true;

        return !string.IsNullOrEmpty(flags) && flags.Contains(letter);
    }

    public bool PlayerImmunityComparison(CCSPlayerController? player, CCSPlayerController target)
    {
        if (player == null) return true;

        if (!AdminUsers.TryGetValue(target.SteamID, out var admin)) return false;

        return AdminUsers[player.SteamID].immunity >= admin.immunity;
    }

    private string BuildConnectionString()
    {
        var dbConfig = Config.Connection!;

        Console.WriteLine("Building connection string");
        var builder = new MySqlConnectionStringBuilder
        {
            Database = dbConfig.Database,
            UserID = dbConfig.User,
            Password = dbConfig.Password,
            Server = dbConfig.Host,
            Port = (uint)dbConfig.Port,
            Pooling = true,
            MinimumPoolSize = 0,
            MaximumPoolSize = 640,
            ConnectionIdleTimeout = 30
        };

        Console.WriteLine("OK!");
        return builder.ConnectionString;
    }

    private void LoadConfig()
    {
        Config = Utils.LoadConfig<Config>("database", ModuleDirectory);
        BaseConfig = Utils.LoadConfig<BaseConfig>("base", ModuleDirectory);
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
        AddTimer(2.0f, () => Server.ExecuteCommand($"{(isWorkshop ? "ds_workshop_changelevel" : "map")} {mapName}"));
    }
    
    public async Task AddBanAsync(CCSPlayerController? admin, CCSPlayerController target, int time, string reason)
    {
        await Server.NextFrameAsync(() =>
        {
            var adminInfo = GetNameAndId(admin);

            var targetId = new SteamID(target.SteamID);
            Database.AddBan(admin, new BanUser
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
    
    public async Task UnBanAsync(CCSPlayerController? admin, string targetSteamId, string reason)
    {
        await Server.NextFrameAsync(() =>
        {
            var adminInfo = GetNameAndId(admin);

            Database.UnbanUser(admin, adminInfo.name, adminInfo.id, targetSteamId, reason);
        });
    }

    public async Task AddMuteAsync(CCSPlayerController? admin, CCSPlayerController target, MuteType type, int time, string reason)
    {
        await Server.NextFrameAsync(() =>
        {
            var adminInfo = GetNameAndId(admin);
            var targetId = new SteamID(target.SteamID);

            var user = new MuteUser
            {
                mute_type = (int)type,
                admin_username = adminInfo.name,
                admin_steamid = adminInfo.id,
                username = target.PlayerName,
                steamid64 = targetId.SteamId64,
                steamid = targetId.SteamId2,
                reason = reason,
                unmute_reason = "",
                admin_unlocked_username = "",
                admin_unlocked_steamid = "",
                start_mute_time = DateTime.UtcNow.GetUnixEpoch(),
                end_mute_time = time == 0 ? 0 : DateTime.UtcNow.AddSeconds(time).GetUnixEpoch(),
                mute_active = true
            };

            Database.AddMute(admin, user);
            MuteUsers[target.SteamID] = user;
        });
    }
    
    public async Task UnMuteAsync(CCSPlayerController? admin, MuteType type, string targetSteamId, string reason)
    {
        await Server.NextFrameAsync(() =>
        {
            var adminInfo = GetNameAndId(admin);

            Database.UnmuteUser(admin, (int)type, adminInfo.name, adminInfo.id, targetSteamId, reason);
        });
    }
    
    public void PrintToChat(CCSPlayerController controller, string msg)
    {
        controller.PrintToChat($"{Localizer["prefix"]} {msg}");
    }

    public void PrintToCenterAll(string msg)
    {
        VirtualFunctions.ClientPrintAll(HudDestination.Center, $"Admin: {msg}", 0, 0, 0, 0);
    }

    public void PrintToChatAll(string msg)
    {
        Server.PrintToChatAll($"{Localizer["prefix"]} {msg}");
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

public class BaseConfig
{
    public List<string> Maps { get; set; } = new();
    public List<string> BanReasons { get; set; } = new();
    public List<string> MuteReasons { get; set; } = new();
    public List<int> Immunity { get; set; } = new();
    public Dictionary<string, int> Times { get; set; } = new();
}

public class Config
{
    public bool UseCenterHtmlMenu { get; init; } = true;
    public string BanFromConsoleName { get; init; } = "Console";
    public BaseAdminDb? Connection { get; init; }
}

public class BaseAdminDb
{
    public required string Host { get; init; } = "Host";
    public required string Database { get; init; } = "Database name";
    public required string User { get; init; } = "Username";
    public required string Password { get; init; } = "Password";
    public int Port { get; init; } = 3306;
}