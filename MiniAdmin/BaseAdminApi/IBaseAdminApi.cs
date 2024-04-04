using System;
using System.Threading.Tasks;
using BaseAdminApi.Enums;
using BaseAdminApi.Models;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;

namespace BaseAdminApi;

public interface IBaseAdminApi
{
    public static readonly PluginCapability<IBaseAdminApi?> Capability = new("baseadmin");
    
    void BanPlayer(CCSPlayerController? admin, CCSPlayerController target, int time, string reason);
    Task BanPlayerAsync(CCSPlayerController? admin, CCSPlayerController target, int time, string reason);
    void UnBanPlayer(CCSPlayerController? admin, string steamId, string reason);
    Task UnBanPlayerAsync(CCSPlayerController? admin, string steamId, string reason);
    bool CheckingForAdminAndFlag(CCSPlayerController? player, AdminFlag flag);
    void ReplyToCommand(CCSPlayerController? controller, string message, params object?[] args);
    void RegisterCommand(string command, AdminFlag flag, int args, string usage,
        Action<CCSPlayerController?, CommandInfo> handler);
    void RegisterCommand(string command, AdminFlag flag, Action<CCSPlayerController?, CommandInfo> handler);

    void RegisterMenuItem(string display, Action<CCSPlayerController, Admin> handler, bool disabled = false);
    void RegisterNewMenuItem(MenuItem type, string display, Action<CCSPlayerController, Admin> handler,
        bool disabled = false);
}