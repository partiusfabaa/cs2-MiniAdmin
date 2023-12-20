using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace BaseAdmin;

public class AdminChat
{
    public void SendToAdminChat(CCSPlayerController player, string message)
    {
        player.PrintToChat(
            $"[{ChatColors.Blue} AdminChat {ChatColors.Default}]{message}");
    }
    
    public void SendToAdminChatFromPlayer(CCSPlayerController player, string message)
    {
        player.PrintToChat(
            $"[{ChatColors.Blue} From Players {ChatColors.Default}]{message}");
    }
    
    public void SendToAllFromAdmin(string message)
    {
        Server.PrintToChatAll(
            $"[{ChatColors.Blue}ALL{ChatColors.Default}] {ChatColors.Blue}ADMIN{ChatColors.Default}: {message}");
    }
}