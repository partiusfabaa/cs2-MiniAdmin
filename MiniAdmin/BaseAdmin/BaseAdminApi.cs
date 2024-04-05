using System;
using System.Threading.Tasks;
using BaseAdminApi;
using BaseAdminApi.Enums;
using BaseAdminApi.Models;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;

namespace BaseAdmin;

public class BaseAdminApi : IBaseAdminApi
{
    private readonly BaseAdmin _baseAdmin;

    public BaseAdminApi(BaseAdmin baseAdmin)
    {
        _baseAdmin = baseAdmin;
    }

    public void BanPlayer(CCSPlayerController? admin, CCSPlayerController target, int time, string reason)
    {
        Task.Run(() => _baseAdmin.AddBanAsync(admin, target, time, reason));
    }

    public async Task BanPlayerAsync(CCSPlayerController? admin, CCSPlayerController target, int time, string reason)
    {
        await _baseAdmin.AddBanAsync(admin, target, time, reason);
    }

    public void UnBanPlayer(CCSPlayerController? admin, string steamId, string reason)
    {
        Task.Run(() => _baseAdmin.UnBanAsync(admin, steamId, reason));
    }

    public async Task UnBanPlayerAsync(CCSPlayerController? admin, string steamId, string reason)
    {
        await _baseAdmin.UnBanAsync(admin, steamId, reason);
    }

    public bool IsPlayerMute(CCSPlayerController player, MuteType type = MuteType.All)
    {
        return _baseAdmin.IsPlayerMuted(player.SteamID, type);
    }

    public bool CheckingForAdminAndFlag(CCSPlayerController? player, AdminFlag flag)
    {
        return _baseAdmin.CheckingForAdminAndFlag(player, flag);
    }

    public void ReplyToCommand(CCSPlayerController? controller, string message, params object?[] args)
    {
        _baseAdmin.ReplyToCommand(controller, message, args);
    }
    
    public void RegisterCommand(string command, AdminFlag flag, int args, string usage, Action<CCSPlayerController?, CommandInfo> handler)
    {
        _baseAdmin.CommandManager.RegisterCommand(command, flag, args, usage, handler);
    }

    public void RegisterCommand(string command, AdminFlag flag, Action<CCSPlayerController?, CommandInfo> handler)
    {
        RegisterCommand(command, flag, 0, string.Empty, handler);
    }

    public void RegisterMenuItem(string display, Action<CCSPlayerController, Admin> handler, bool disabled)
    {
        _baseAdmin.Menu.AddMenuOptions(display, (controller, option) =>
        {
            if (!_baseAdmin.AdminUsers.TryGetValue(controller.SteamID, out var value)) return;

            handler(controller, value);
        }, disabled);
    }

    public void RegisterNewMenuItem(MenuItem type, string display, Action<CCSPlayerController, Admin> handler, bool disabled)
    {
        _baseAdmin.Menu.AddMenuOptions(type, display, (controller, option) =>
        {
            if (!_baseAdmin.AdminUsers.TryGetValue(controller.SteamID, out var value)) return;

            handler(controller, value);
        }, disabled);
    }
}