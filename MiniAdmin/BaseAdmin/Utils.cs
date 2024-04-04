using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace BaseAdmin;

public static class Utils
{
    public static void Kick(this CCSPlayerController? player, string reason)
    {
        if (player == null)
        {
            Console.WriteLine("player is null");
            return;
        }

        Kick(player.UserId ?? -1, reason);
    }
    
    public static void Kick(int userId, string reason)
    {
        if (userId == -1) return;
        
        Server.ExecuteCommand(string.Create(CultureInfo.InvariantCulture, $"kickid {userId} \"{reason}\""));
    }

    public static int GetUnixEpoch(this DateTime dateTime)
    {
        var unixTime = dateTime.ToUniversalTime() -
                       new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return (int)unixTime.TotalSeconds;
    }

    public static T LoadConfig<T>(string name, string path)
    {
        var configFilePath = Path.Combine(path, $"{name}.json");

        if (!File.Exists(configFilePath))
        {
            var defaultConfig = Activator.CreateInstance<T>();
            var defaultJson =
                JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configFilePath, defaultJson);
        }

        var configJson = File.ReadAllText(configFilePath);
        var config = JsonSerializer.Deserialize<T>(configJson);

        if (config == null)
            throw new FileNotFoundException($"File {name}.json not found or cannot be deserialized");

        return config;
    }

    public static bool GetPlayer(string id, [MaybeNullWhen(false)] out CCSPlayerController player)
    {
        player = null;
        var players = Utilities.GetPlayers();
        if (id.StartsWith("STEAM_"))
        {
            player = players.FirstOrDefault(p =>
                p.AuthorizedSteamID != null && p.AuthorizedSteamID.SteamId2.Contains(id));
            return player != null;
        }

        if (id.StartsWith('#') && int.TryParse(id.Trim('#'), out var userid))
        {
            player = Utilities.GetPlayerFromUserid(userid);
            return true;
        }

        player = players.FirstOrDefault(u => u.PlayerName.Contains(id));
        return player != null;
    }
}