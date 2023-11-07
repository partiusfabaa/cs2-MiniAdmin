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
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using Dapper;
using MySqlConnector;

namespace MiniAdmin;

public class MiniAdmin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "Mini Admin";
    public override string ModuleVersion => "v1.0.3";

    private string _dbConnectionString = string.Empty;

    private DateTime[] _playerPlayTime = new DateTime[Server.MaxPlayers];

    private static readonly string Prefix = "[\x0C Admin Menu \x01]";
    private readonly ChatMenu _slayMenu = new(Prefix + " Slay");

    public override void Load(bool hotReload)
    {
        _dbConnectionString = BuildConnectionString();
        Task.Run(() => CreateTable(_dbConnectionString));
        Task.Run(() => CreateAdminsTable(_dbConnectionString));

        RegisterListener<Listeners.OnClientConnected>(slot =>
        {
            var entity = NativeAPI.GetEntityFromIndex(slot + 1);

            if (entity == IntPtr.Zero) return;

            var player = new CCSPlayerController(entity);

            Task.Run(() => OnClientConnectedAsync(slot, player, new SteamID(player.SteamID)));
        });

        RegisterListener<Listeners.OnClientDisconnectPost>(slot => { _playerPlayTime[slot + 1] = DateTime.MinValue; });

        AddTimer(300, () =>
        {
            Task.Run(Timer_DeleteAdminAsync);
        }, TimerFlags.REPEAT);
        CreateMenu();
    }

    private async Task Timer_DeleteAdminAsync()
    {
        await using var connection = new MySqlConnection(_dbConnectionString);
            
        var deleteAdmins = await connection.QueryAsync<Admins>(
            "SELECT * FROM miniadmin_admins WHERE EndTime <= @CurrentTime",
            new { CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });

        var adminsEnumerable = deleteAdmins.ToList();
        if (adminsEnumerable.Any())
        {
            foreach (var result in adminsEnumerable.Select(deleteAdmin => DeleteAdmin(deleteAdmin.SteamId)))
                PrintToServer(await result, ConsoleColor.DarkMagenta);
        }
    }

    private async Task OnClientConnectedAsync(int slot, CCSPlayerController player, SteamID steamId)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            var unbanUsers = await connection.QueryAsync<User>(
                "SELECT * FROM miniadmin_bans WHERE EndBanTime <= @CurrentTime AND BanActive = 1",
                new { CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });

            foreach (var user in unbanUsers)
            {
                PrintToServer($"Unban: {user.SteamId}", ConsoleColor.DarkMagenta);
                await UnbanUser("Console", "Console", user.SteamId, "The deadline has passed");
            }

            await Timer_DeleteAdminAsync();

            var banUser = await connection.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM miniadmin_bans WHERE SteamId64 = @SteamId64 AND BanActive = 1",
                new { steamId.SteamId64 });

            if (banUser != null) Server.ExecuteCommand($"kick {player.PlayerName}");
            else
                _playerPlayTime[slot + 1] = DateTime.Now;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private void CreateMenu()
    {
        var adminMenu = new ChatMenu(Prefix);
        adminMenu.AddMenuOption("Slay players", (player, option) =>
        {
            if (!IsAdmin(player))
            {
                PrintToChat(player, "you do not have access to this command");
                return;
            }

            CreateSlayMenu();
            ChatMenus.OpenMenu(player, _slayMenu);
        });

        AddCommand("css_admin", "admin menu", (player, info) =>
        {
            if (player == null) return;
            if (!IsAdmin(player))
            {
                PrintToChat(player, "you do not have access to this command");
                return;
            }

            ChatMenus.OpenMenu(player, adminMenu);
        });
    }

    private void CreateSlayMenu()
    {
        var playerEntities = Utilities.GetPlayers();
        _slayMenu.MenuOptions.Clear();
        _slayMenu.AddMenuOption("All", (controller, option) =>
        {
            foreach (var player in playerEntities) player.PlayerPawn.Value.CommitSuicide(true, true);
        });
        foreach (var player in playerEntities)
        {
            if(!player.PawnIsAlive) continue;
            
            _slayMenu.AddMenuOption($"{player.PlayerName} [{player.EntityIndex!.Value.Value}]", (controller, option) =>
            {
                var parts = option.Text.Split('[', ']');
                if (parts.Length < 2) return;
                var target = Utilities.GetPlayerFromIndex(int.Parse(parts[1]));
                    
                if (!target.PawnIsAlive)
                {
                    PrintToChat(controller, "The player is already dead");
                    return;
                }

                target.PlayerPawn.Value.CommitSuicide(true, true);

                PrintToChatAll($"{controller.PlayerName}: Player '{target.PlayerName}' has been killed");
            });
        }
    }

    static async Task CreateTable(string connectionString)
    {
        try
        {
            await using var dbConnection = new MySqlConnection(connectionString);
            dbConnection.Open();

            var createBansTable = @"
            CREATE TABLE IF NOT EXISTS `miniadmin_bans` (
                `Id` INT AUTO_INCREMENT PRIMARY KEY,
                `AdminUsername` VARCHAR(255) NOT NULL,
                `AdminSteamId` VARCHAR(255) NOT NULL,
                `Username` VARCHAR(255) NOT NULL,
                `SteamId64` BIGINT NOT NULL,
                `SteamId` VARCHAR(255) NOT NULL,
                `Reason` VARCHAR(255) NOT NULL,
                `UnbanReason` VARCHAR(255) NOT NULL,
                `AdminUnlockedUsername` VARCHAR(255) NOT NULL,
                `AdminUnlockedSteamId` VARCHAR(255) NOT NULL,
                `StartBanTime` BIGINT NOT NULL,
                `EndBanTime` BIGINT NOT NULL,
                `BanActive` BOOLEAN NOT NULL
            );";

            await dbConnection.ExecuteAsync(createBansTable);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    static async Task CreateAdminsTable(string connectionString)
    {
        try
        {
            await using var connection = new MySqlConnection(connectionString);

            connection.Open();

            var createAdminsTable = @"
                CREATE TABLE IF NOT EXISTS `miniadmin_admins` (
                `Id` INT AUTO_INCREMENT PRIMARY KEY,
                `Username` VARCHAR(255) NOT NULL,
                `SteamId` VARCHAR(255) NOT NULL,
                `StartTime` BIGINT NOT NULL,
                `EndTime` BIGINT NOT NULL
            );";

            await connection.ExecuteAsync(createAdminsTable);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    [ConsoleCommand("css_who")]
    public void OnCmdWho(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller != null) return;

        var maxNameLength = 0;

        var id = 0;
        for (var i = 1; i < 64; i++)
        {
            var client = Utilities.GetPlayerFromIndex(i);

            var playerName = !string.IsNullOrWhiteSpace(client.PlayerName) ? client.PlayerName : "unknown";
            var playerNameLength = playerName.Length;
            maxNameLength = Math.Max(maxNameLength, playerNameLength);

            var adminStatus = IsAdmin(client) ? "admin " : "player";

            var index = client.EntityIndex!.Value.Value;
            var playTime = DateTime.Now - _playerPlayTime[index]; 

            id++;
            var formattedOutput =
                $"{id,-1} - {playerName,-15} | {adminStatus,-6} | Playtime: {playTime.Hours:D2}:{playTime.Minutes:D2}:{playTime.Seconds:D2}";

            PrintToServer(formattedOutput, ConsoleColor.Magenta);
        }
    }

    [ConsoleCommand("css_map", "change map")]
    public void OnCmdChangeMap(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null) return;

        var cmdArg = command.ArgString;

        if (!IsAdmin(controller))
        {
            PrintToChat(controller, "you do not have access to this command");
            return;
        }

        var mapText = File.ReadAllText(Path.Combine(ModuleDirectory, "maps.txt"));
        var mapList = mapText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).ToArray();

        foreach (var t in mapList)
        {
            if (t.Trim().Contains(cmdArg.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                PrintToChatAll($"{controller.PlayerName}: changing the map to {t}");
                AddTimer(3.0f, () => ChangeMap(t));
                return;
            }
        }

        PrintToChat(controller, "This map doesn't exist");
    }


    [ConsoleCommand("css_slay", "kill a player")]
    public void OnCmdSlay(CCSPlayerController? controller, CommandInfo command)
    {
        var cmdArg = command.ArgString;

        if (controller != null)
            if (!IsAdmin(controller))
            {
                PrintToChat(controller, "you do not have access to this command");
                return;
            }

        if (command.ArgCount is < 2 or > 2)
        {
            ReplyToCommand(controller, "Using: css_slay <userid>");
            return;
        }

        var entity = NativeAPI.GetEntityFromIndex(Convert.ToInt32(cmdArg) + 1);

        if (entity == IntPtr.Zero)
        {
            ReplyToCommand(controller, "Player not found");
            return;
        }

        var target = new CCSPlayerController(entity);
        target.PlayerPawn.Value.CommitSuicide(true, true);

        if (controller == null)
            PrintToChatAll($"Console: Player '{target.PlayerName}' has been killed");

        ReplyToCommand(controller,
            $"{(controller != null ? controller.PlayerName : "Console")}: Player '{target.PlayerName}' has been killed");
    }

    [ConsoleCommand("css_kick", "Kick a player")]
    public void OnCmdKick(CCSPlayerController? controller, CommandInfo command)
    {
        var cmdArg = command.ArgString;

        if (controller != null)
            if (!IsAdmin(controller))
            {
                PrintToChat(controller, "you do not have access to this command");
                return;
            }

        if (command.ArgCount is < 2 or > 2)
        {
            ReplyToCommand(controller, "Using: css_kick <userid>");
            return;
        }

        var convertCmdArg = Convert.ToInt32(cmdArg);

        var entity = NativeAPI.GetEntityFromIndex(convertCmdArg + 1);
        var userId = NativeAPI.GetUseridFromIndex(convertCmdArg + 1);

        if (entity == IntPtr.Zero)
        {
            ReplyToCommand(controller, "Player not found");
            return;
        }

        var target = new CCSPlayerController(entity);

        KickClient($"{userId}");

        var msg =
            $"{(controller != null ? controller.PlayerName : "Console")}: Player '{target.PlayerName}' kicked by admin";
        ReplyToCommand(controller, msg);
    }

    [ConsoleCommand("css_ban", "ban")]
    public void OnCmdBan(CCSPlayerController? controller, CommandInfo command)
    {
        var cmdArg = command.ArgString;

        if (controller != null)
            if (!IsAdmin(controller))
            {
                PrintToChat(controller, "you do not have access to this command");
                return;
            }

        if (command.ArgCount is < 4 or > 4)
        {
            ReplyToCommand(controller, "Using: css_ban <userid> <time_seconds> <reason>");
            return;
        }

        var splitCmdArgs = Regex.Matches(cmdArg, @"[\""].+?[\""]|[^ ]+")
            .Select(m => m.Value)
            .ToArray();

        var convertCmdArg = Convert.ToInt32(ExtractValueInQuotes(splitCmdArgs[0]));

        var entity = NativeAPI.GetEntityFromIndex(convertCmdArg + 1);
        var userId = NativeAPI.GetUseridFromIndex(convertCmdArg + 1);

        if (entity == IntPtr.Zero)
        {
            ReplyToCommand(controller, "Player not found");
            return;
        }

        var target = new CCSPlayerController(entity);

        var endBanTime = Convert.ToInt32(ExtractValueInQuotes(splitCmdArgs[1]));
        var reason = ExtractValueInQuotes(splitCmdArgs[2]);

        Console.WriteLine($"ExtractValue: {endBanTime}");
        Console.WriteLine($"Split: {splitCmdArgs[0]} + {splitCmdArgs[1]} + {splitCmdArgs[2]}");
        
        var startBanTimeUnix = DateTime.UtcNow.GetUnixEpoch();
        var endBanTimeUnix = DateTime.UtcNow.AddSeconds(endBanTime).GetUnixEpoch();
        
        var msg = Task.Run(() => AddBan(new User
        {
            AdminUsername = controller != null ? controller.PlayerName : "Console",
            AdminSteamId = controller != null ? new SteamID(controller.SteamID).SteamId2 : "Console",
            Username = target.PlayerName,
            SteamId64 = target.SteamID,
            SteamId = new SteamID(target.SteamID).SteamId2,
            Reason = reason,
            UnbanReason = "",
            AdminUnlockedUsername = "",
            AdminUnlockedSteamId = "",
            StartBanTime = startBanTimeUnix,
            EndBanTime = endBanTime == 0 ? 0 : endBanTimeUnix,
            BanActive = true
        })).Result;

        KickClient($"{userId}");

        ReplyToCommand(controller, msg);
    }

    [ConsoleCommand("css_addadmin")]
    public void OnCmdAddAdmin(CCSPlayerController? controller, CommandInfo command)
    {
        if(controller != null) return;
    
        var cmdArg = command.ArgString;

        if (command.ArgCount is < 4 or > 4)
        {
            PrintToServer("Using: css_addadmin <username> <steamid> <time_seconds>", ConsoleColor.Red);
            return;
        }
        
        var splitCmdArgs = Regex.Matches(cmdArg, @"[\""].+?[\""]|[^ ]+")
            .Select(m => m.Value)
            .ToArray();

        var username = ExtractValueInQuotes(splitCmdArgs[0]);
        var steamId = ExtractValueInQuotes(splitCmdArgs[1]);
        var endTime = Convert.ToInt32(ExtractValueInQuotes(splitCmdArgs[2]));
        
        var startTimeUnix = DateTime.UtcNow.GetUnixEpoch();
        var endTimeUnix = DateTime.UtcNow.AddSeconds(endTime).GetUnixEpoch();

        var msg = Task.Run(() => AddAdmin(new Admins
        {
            Username = username,
            SteamId = steamId,
            StartTime = startTimeUnix,
            EndTime = endTime == 0 ? 0 : endTimeUnix
        })).Result;

        PrintToServer(msg, ConsoleColor.Green);
    }

    private async Task<string> AddBan(User user)
    {
        try
        {
            if (IsUserBanned(user.SteamId))
                return $"The user with the SteamId identifier {user.SteamId} has already been banned.";

            await using var connection = new MySqlConnection(_dbConnectionString);

            await connection.ExecuteAsync(@"
                INSERT INTO miniadmin_bans (AdminUsername, AdminSteamId, Username, SteamId64, SteamId, Reason, UnbanReason, AdminUnlockedUsername, AdminUnlockedSteamId, StartBanTime, EndBanTime, BanActive)
                VALUES (@AdminUsername, @AdminSteamId, @Username, @SteamId64, @SteamId, @Reason, @UnbanReason, @AdminUnlockedUsername, @AdminUnlockedSteamId, @StartBanTime, @EndBanTime, @BanActive);
                ", user);

            return $"Player '{user.Username} | [{user.SteamId}]' is banned";
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return string.Empty;
    }

    private async Task<string> AddAdmin(Admins admin)
    {
        try
        {
            if (IsAdminExist(admin.SteamId))
                return $"An administrator with the SteamId identifier {admin.SteamId} already exists.";

            await using var connection = new MySqlConnection(_dbConnectionString);

            await connection.ExecuteAsync(@"INSERT INTO miniadmin_admins (Username, SteamId, StartTime, EndTime)
                        VALUES (@Username, @SteamId, @StartTime, @EndTime);", admin);

            return $"Admin '{admin.Username}[{admin.SteamId}]' successfully added";
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return string.Empty;
    }

    [ConsoleCommand("css_unban", "unban")]
    public void OnCmdUnban(CCSPlayerController? controller, CommandInfo command)
    {
        var cmdArg = command.ArgString;
        
        if (controller != null && !IsAdmin(controller))
        {
            PrintToChat(controller, "you do not have access to this command");
            return;
        }

        if (command.ArgCount is < 3 or > 3)
        {
            ReplyToCommand(controller, "Using: css_unban <SteamId> <Reason>");
            return;
        }
        
        var splitCmdArgs = Regex.Matches(cmdArg, @"[\""].+?[\""]|[^ ]+")
            .Select(m => m.Value)
            .ToArray();

        var steamId = ExtractValueInQuotes(splitCmdArgs[0]);
        var reason = ExtractValueInQuotes(splitCmdArgs[1]);

        var msg = Task.Run(() => UnbanUser(
            controller != null ? controller.PlayerName : "Console",
            controller != null ? new SteamID(controller.SteamID).SteamId2 : "Console",
            steamId, reason)).Result;
    
        ReplyToCommand(controller, msg); 
    }

    [ConsoleCommand("css_deleteadmin", "delete admin")]
    public void OnCmdDeleteAdmin(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller != null) return;
        
        var cmdArg = command.ArgString;

        if (command.ArgCount is < 2 or > 2)
        {
            PrintToServer("Using: css_deleteadmin <SteamId>", ConsoleColor.Red);
            return;
        }

        var steamId = ExtractValueInQuotes(cmdArg);
        
        var msg = Task.Run(() => DeleteAdmin(steamId)).Result;

        PrintToServer(msg, ConsoleColor.Green);
    }

    private async Task<string> DeleteAdmin(string steamId)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            await connection.ExecuteAsync(@"DELETE FROM miniadmin_admins WHERE SteamId = @SteamId;",
                new { SteamId = steamId });

            return $"Admin {steamId} successfully deleted";
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return string.Empty;
    }

    private async Task<string> UnbanUser(string adminName, string adminSteamId, string steamId, string reason)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            var user = await connection.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM miniadmin_bans WHERE SteamId = @SteamId AND BanActive = 1",
                new { SteamId = steamId });

            if (user == null) return "User not found or not currently banned";

            user.UnbanReason = reason;
            user.AdminUnlockedUsername = adminName;
            user.AdminUnlockedSteamId = adminSteamId;
            user.BanActive = false;

            await connection.ExecuteAsync(@"
                    UPDATE miniadmin_bans
                    SET UnbanReason = @UnbanReason, AdminUnlockedUsername = @AdminUnlockedUsername,
                        AdminUnlockedSteamId = @AdminUnlockedSteamId, BanActive = @BanActive
                    WHERE SteamId = @SteamId AND BanActive = 1
                    ", user);

            return $"Player {steamId} has been successfully unblocked with reason: {reason}";
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return string.Empty;
    }

    private Config LoadConfig()
    {
        var configPath = Path.Combine(ModuleDirectory, "database.json");
        if (!File.Exists(configPath)) return CreateConfig(configPath);

        var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath))!;

        return config;
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

    private Config CreateConfig(string configPath)
    {
        var config = new Config
        {
            Connection = new MiniAdminDb
            {
                Host = "",
                Database = "",
                User = "",
                Password = ""
            }
        };

        File.WriteAllText(configPath,
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        return config;
    }

    private string ExtractValueInQuotes(string input)
    {
        var match = Regex.Match(input, @"""([^""]*)""");

        return match.Success ? match.Groups[1].Value : input;
    }

    private void ReplyToCommand(CCSPlayerController? controller, string msg)
    {
        if (controller != null)
            PrintToChat(controller, msg);
        else
            PrintToServer(msg, ConsoleColor.DarkMagenta);
    }

    private void ChangeMap(string mapName)
    {
        Server.ExecuteCommand($"map {mapName}");
    }

    private void KickClient(string userId)
    {
        Server.ExecuteCommand($"kickid {userId}");
    }

    private void PrintToChat(CCSPlayerController controller, string msg)
    {
        controller.PrintToChat($"\x08[ \x0CMiniAdmin \x08] {msg}");
    }

    private void PrintToChatAll(string msg)
    {
        Server.PrintToChatAll($"\x08[ \x0CMiniAdmin \x08] {msg}");
    }

    private void PrintToConsole(CCSPlayerController client, string msg)
    {
        client.PrintToConsole(msg);
    }

    private void PrintToServer(string msg, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine($"[ MiniAdmin ] {msg}");
        Console.ResetColor();
    }

    private bool IsAdmin(CCSPlayerController controller)
    {
        var steamId = new SteamID(controller.SteamID).SteamId2;

        using var connection = new MySqlConnection(_dbConnectionString);

        var admin = connection.QueryFirstOrDefault<Admins>(
            "SELECT * FROM miniadmin_admins WHERE SteamId = @SteamId",
            new { SteamId = steamId });

        return admin != null;
    }

    private bool IsUserBanned(string steamId)
    {
        using var connection = new MySqlConnection(_dbConnectionString);

        var existingBan = connection.QueryFirstOrDefault<User>(
            "SELECT * FROM miniadmin_bans WHERE SteamId = @SteamId AND BanActive = 1",
            new { SteamId = steamId });

        return existingBan != null;
    }

    private bool IsAdminExist(string steamId)
    {
        using var connection = new MySqlConnection(_dbConnectionString);

        var existingAdmin = connection.QueryFirstOrDefault<Admins>(
            "SELECT * FROM miniadmin_admins WHERE SteamId = @SteamId",
            new { SteamId = steamId });

        return existingAdmin != null;
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
    public MiniAdminDb Connection { get; set; } = null!;
}

public class MiniAdminDb
{
    public required string Host { get; init; }
    public required string Database { get; init; }
    public required string User { get; init; }
    public required string Password { get; init; }
}

public class User
{
    public required string AdminUsername { get; set; }
    public required string AdminSteamId { get; set; }
    public required string Username { get; set; }
    public ulong SteamId64 { get; set; }
    public required string SteamId { get; set; }
    public required string Reason { get; set; }
    public required string UnbanReason { get; set; }
    public required string AdminUnlockedUsername { get; set; }
    public required string AdminUnlockedSteamId { get; set; }
    public int StartBanTime { get; set; }
    public int EndBanTime { get; set; }
    public bool BanActive { get; set; }
}

public class Admins
{
    public required string Username { get; set; }
    public required string SteamId { get; set; }
    public int StartTime { get; set; }
    public int EndTime { get; set; }
}