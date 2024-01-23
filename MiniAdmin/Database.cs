using System;
using System.Linq;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using Dapper;
using MySqlConnector;

namespace BaseAdmin;

public class Database
{
    private readonly string _dbConnectionString;
    private readonly BaseAdmin _baseAdmin;

    public Database(BaseAdmin baseAdmin, string dbConnectionString)
    {
        _baseAdmin = baseAdmin;
        _dbConnectionString = dbConnectionString;
    }

    public async Task CreateTable()
    {
        try
        {
            await using var dbConnection = new MySqlConnection(_dbConnectionString);
            dbConnection.Open();

            var createBansTable = @"
                CREATE TABLE IF NOT EXISTS `miniadmin_bans` (
                    `id` INT AUTO_INCREMENT PRIMARY KEY,
                    `admin_username` VARCHAR(255) NOT NULL,
                    `admin_steamid` VARCHAR(255) NOT NULL,
                    `username` VARCHAR(255) NOT NULL,
                    `steamid64` BIGINT NOT NULL,
                    `steamid` VARCHAR(255) NOT NULL,
                    `reason` VARCHAR(255) NOT NULL,
                    `unban_reason` VARCHAR(255) NOT NULL,
                    `admin_unlocked_username` VARCHAR(255) NOT NULL,
                    `admin_unlocked_steamid` VARCHAR(255) NOT NULL,
                    `start_ban_time` BIGINT NOT NULL,
                    `end_ban_time` BIGINT NOT NULL,
                    `ban_active` BOOLEAN NOT NULL
            );";

            await dbConnection.ExecuteAsync(createBansTable);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task CreateAdminsTable()
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            connection.Open();

            var createAdminsTable = @"
                CREATE TABLE IF NOT EXISTS `miniadmin_admins` (
                    `id` INT AUTO_INCREMENT PRIMARY KEY,
                    `username` VARCHAR(255) NOT NULL,
                    `steamid` VARCHAR(255) NOT NULL,
                    `start_time` BIGINT NOT NULL,
                    `end_time` BIGINT NOT NULL,
                    `immunity` INT NOT NULL,
                    `flags` VARCHAR(255) NOT NULL
            );";

            await connection.ExecuteAsync(createAdminsTable);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task CreateMuteTable()
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            connection.Open();

            var createMuteTable = @"
                CREATE TABLE IF NOT EXISTS `miniadmin_mute` (
                    `id` INT AUTO_INCREMENT PRIMARY KEY,
                    `mute_type` INT NOT NULL,
                    `admin_username` VARCHAR(255) NOT NULL,
                    `admin_steamid` VARCHAR(255) NOT NULL,
                    `username` VARCHAR(255) NOT NULL,
                    `steamid64` BIGINT NOT NULL,
                    `steamid` VARCHAR(255) NOT NULL,
                    `reason` VARCHAR(255) NOT NULL,
                    `unmute_reason` VARCHAR(255) NOT NULL,
                    `admin_unlocked_username` VARCHAR(255) NOT NULL,
                    `admin_unlocked_steamid` VARCHAR(255) NOT NULL,
                    `start_mute_time` BIGINT NOT NULL,
                    `end_mute_time` BIGINT NOT NULL,
                    `mute_active` BOOLEAN NOT NULL
            );";

            await connection.ExecuteAsync(createMuteTable);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task<string> AddBan(User user)
    {
        try
        {
            var isUserBanned = await IsUserBanned(user.steamid);
            if (isUserBanned)
                return $"The user with the SteamId identifier {user.steamid} has already been banned.";

            await using var connection = new MySqlConnection(_dbConnectionString);

            await connection.ExecuteAsync(@"
                INSERT INTO miniadmin_bans (admin_username, admin_steamid, username, steamid64, steamid, reason, unban_reason, admin_unlocked_username, admin_unlocked_steamid, start_ban_time, end_ban_time, ban_active)
                VALUES (@admin_username, @admin_steamid, @username, @steamid64, @steamid, @reason, @unban_reason, @admin_unlocked_username, @admin_unlocked_steamid, @start_ban_time, @end_ban_time, @ban_active);
                ", user);

            return $"Player '{user.username} | [{user.steamid}]' is banned";
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return string.Empty;
    }

    public async Task AddMute(MuteUser user, CCSPlayerController? controller)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            var activeUserMute = await GetActiveMuteAsync(user.steamid);

            if (activeUserMute != null)
            {
                if (activeUserMute.mute_type == user.mute_type)
                {
                    Server.NextFrame(() =>
                        _baseAdmin.ReplyToCommand(controller, "The user already has sound or chat disabled"));
                    return;
                }

                if (activeUserMute.mute_type == 2 || user.mute_type == 2)
                {
                    Server.NextFrame(() => _baseAdmin.ReplyToCommand(controller,
                        $"The user with the SteamId identifier {user.steamid} has already been muted in all channels."));
                    return;
                }

                if (activeUserMute.mute_type is 1 or 0 && user.mute_type is 1 or 0)
                    user.mute_type = 2;

                await connection.ExecuteAsync(@"
                UPDATE miniadmin_mute
                SET mute_type = @mute_type,
                    admin_username = @admin_username,
                    admin_steamid = @admin_steamid,
                    username = @username,
                    steamid64 = @steamid64,
                    steamid = @steamid,
                    reason = @reason,
                    unmute_reason = @unmute_reason,
                    admin_unlocked_username = @admin_unlocked_username,
                    admin_unlocked_steamid = @admin_unlocked_steamid,
                    start_mute_time = @start_mute_time,
                    end_mute_time = @end_mute_time,
                    mute_active = @mute_active
                WHERE steamid = @steamid and mute_active = 1;
                ", user);

                Server.NextFrame(() => _baseAdmin.ReplyToCommand(controller,
                    $"Player '{user.username} | [{user.steamid}]' mute has been updated."));
                return;
            }

            await connection.ExecuteAsync(@"
            INSERT INTO miniadmin_mute (mute_type, admin_username, admin_steamid, username, steamid64, steamid, reason, unmute_reason, admin_unlocked_username, admin_unlocked_steamid, start_mute_time, end_mute_time, mute_active)
            VALUES (@mute_type, @admin_username, @admin_steamid, @username, @steamid64, @steamid, @reason, @unmute_reason, @admin_unlocked_username, @admin_unlocked_steamid, @start_mute_time, @end_mute_time, @mute_active);
            ", user);

            Server.NextFrame(() =>
                _baseAdmin.ReplyToCommand(controller, $"Player '{user.username} | [{user.steamid}]' is muted."));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task AddAdmin(Admins admin, CCSPlayerController? player = null, bool fromMenu = false)
    {
        try
        {
            var isAdminExist = await GetAdminFromDb(admin.steamid);
            if (isAdminExist != null)
            {
                _baseAdmin.PrintLogError("An administrator with the SteamId identifier {steamid} already exists.",
                    admin.steamid);

                if (fromMenu)
                {
                    if(player != null)
                        Server.NextFrame(() => _baseAdmin.PrintToChat(player, $"An administrator with the SteamId identifier {admin.steamid} already exists."));
                }
                return;
            }

            await using var connection = new MySqlConnection(_dbConnectionString);

            await connection.ExecuteAsync(@"
                INSERT INTO 
                    miniadmin_admins (username, steamid, start_time, end_time, immunity, flags)
                VALUES 
                    (@username, @steamid, @start_time, @end_time, @immunity, @flags);", admin);

            _baseAdmin.PrintLogInfo("Admin '{username}[{steamid}]' successfully added",
                admin.username, admin.steamid);
            if (fromMenu)
            {
                if (player != null)
                {
                    Server.NextFrame(() => _baseAdmin.PrintToChat(player, $"Admin '{admin.username}[{admin.steamid}]' successfully added"));
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task<string> DeleteExpiredAdminsAsync()
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            var deleteAdmins = await connection.QueryAsync<Admins>(
                "SELECT * FROM miniadmin_admins WHERE end_time <= @CurrentTime AND end_time > 0",
                new { CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });

            var adminsEnumerable = deleteAdmins.ToList();
            if (adminsEnumerable.Any())
            {
                foreach (var deleteAdmin in adminsEnumerable)
                {
                    await connection.ExecuteAsync(@"DELETE FROM miniadmin_admins WHERE steamid = @SteamId;",
                        new { SteamId = deleteAdmin.steamid });

                    _baseAdmin.PrintLogInfo("Admin {steamid} successfully deleted",
                        deleteAdmin.steamid);
                }

                _baseAdmin.PrintLogInfo("Expired admins successfully deleted");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return string.Empty;
    }

    public async Task<string> DeleteAdminAsync(string steamId)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            await connection.ExecuteAsync(@"DELETE FROM miniadmin_admins WHERE steamid = @SteamId;",
                new { SteamId = steamId });

            _baseAdmin.PrintLogInfo("Admin {steamId} successfully deleted", steamId);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return string.Empty;
    }

    public async Task<string> UnbanUser(string adminName, string adminSteamId, string steamId, string reason)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            var user = await connection.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM miniadmin_bans WHERE steamid = @SteamId AND ban_active = 1",
                new { SteamId = steamId });

            if (user == null) return "User not found or not currently banned";

            user.unban_reason = reason;
            user.admin_unlocked_username = adminName;
            user.admin_unlocked_steamid = adminSteamId;
            user.ban_active = false;

            await connection.ExecuteAsync(@"
                    UPDATE miniadmin_bans
                    SET unban_reason = @unban_reason, admin_unlocked_username = @admin_unlocked_username,
                        admin_unlocked_steamid = @admin_unlocked_steamid, ban_active = @ban_active
                    WHERE steamid = @SteamId AND ban_active = 1
                    ", user);

            return $"Player {steamId} has been successfully unblocked with reason: {reason}";
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return string.Empty;
    }

    public async Task<string> UnmuteUser(int unmuteType, string adminName, string adminSteamId, string steamId,
        string reason)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            var user = await connection.QueryFirstOrDefaultAsync<MuteUser>(
                "SELECT * FROM miniadmin_mute WHERE steamid = @SteamId AND mute_active = 1",
                new { SteamId = steamId });

            if (user == null) return "User not found or not currently muted";

            user.unmute_reason = reason;
            user.admin_unlocked_username = adminName;
            user.admin_unlocked_steamid = adminSteamId;

            if (unmuteType != -1)
            {
                if (user.mute_type is 2 && unmuteType is 0)
                    user.mute_type = 1;
                else if (user.mute_type is 2 && unmuteType is 1)
                    user.mute_type = 0;
                else if (user.mute_type == unmuteType)
                    user.mute_active = false;
            }
            else
                user.mute_active = false;

            await connection.ExecuteAsync(@"
                    UPDATE miniadmin_mute
                    SET 
                        mute_type = @mute_type,
                        unmute_reason = @unmute_reason, 
                        admin_unlocked_username = @admin_unlocked_username,
                        admin_unlocked_steamid = @admin_unlocked_steamid, 
                        mute_active = @mute_active
                    WHERE 
                        steamid = @SteamId 
                      AND
                        mute_active = 1
                    ", user);

            return $"Player {steamId} has been successfully unmuted with reason: {reason}";
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return string.Empty;
    }

    public async Task<MuteUser?> GetActiveMuteAsync(string steamId)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            var muteData = await connection.QueryFirstOrDefaultAsync<MuteUser>(@"
            SELECT * FROM miniadmin_mute
            WHERE steamid = @SteamId AND mute_active = 1;
            ", new { SteamId = steamId });

            return muteData;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }

    private async Task<bool> IsUserBanned(string steamId)
    {
        await using var connection = new MySqlConnection(_dbConnectionString);

        var existingBan = connection.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM miniadmin_bans WHERE steamid = @SteamId AND ban_active = 1",
            new { SteamId = steamId }).Result;

        return existingBan != null;
    }

    public async Task<bool> IsUserMuted(string steamId)
    {
        await using var connection = new MySqlConnection(_dbConnectionString);

        var existingMute = connection.QueryFirstOrDefaultAsync<MuteUser>(
            "SELECT * FROM miniadmin_mute WHERE steamid = @SteamId AND mute_active = 1",
            new { SteamId = steamId }).Result;

        return existingMute != null;
    }

    public async Task<Admins?> GetAdminFromDb(string steamId)
    {
        await using var connection = new MySqlConnection(_dbConnectionString);

        var existingAdmin = connection.QueryFirstOrDefaultAsync<Admins>(
            "SELECT * FROM miniadmin_admins WHERE steamid = @SteamId",
            new { SteamId = steamId }).Result;

        return existingAdmin;
    }

    // private async Task<bool> IsAdminExist(string steamId)
    // {
    //     await using var connection = new MySqlConnection(_dbConnectionString);
    //
    //     var existingAdmin = connection.QueryFirstOrDefaultAsync<Admins>(
    //         "SELECT * FROM miniadmin_admins WHERE steamid = @SteamId",
    //         new { SteamId = steamId }).Result;
    //
    //     return existingAdmin != null;
    // }
}