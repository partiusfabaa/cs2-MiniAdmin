using System;
using System.Globalization;
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
        
        Server.ExecuteCommand(string.Create(CultureInfo.InvariantCulture, $"kickid {player.UserId!.Value} \"{reason}\""));
    }
    
    public static int GetUnixEpoch(this DateTime dateTime)
    {
        var unixTime = dateTime.ToUniversalTime() -
                       new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return (int)unixTime.TotalSeconds;
    }
}