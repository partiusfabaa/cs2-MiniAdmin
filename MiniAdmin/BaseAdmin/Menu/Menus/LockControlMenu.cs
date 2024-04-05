using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseAdminApi.Enums;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace BaseAdmin.Menu.Menus;

public class LockControlMenu : AdminMenuBase
{
    private readonly BaseAdmin _baseAdmin;
    private readonly MenuService _menuService;

    public LockControlMenu(BaseAdmin baseAdmin, MenuService menuService) : base(baseAdmin, menuService)
    {
        _baseAdmin = baseAdmin;
        _menuService = menuService;
    }

    public override void Handle(CCSPlayerController player, ChatMenuOption option)
    {
        var menu = _menuService.CreateMenu(_baseAdmin.Localizer["menu.lock_control"]);

        var baseConfig = _baseAdmin.BaseConfig;
        if (_baseAdmin.CheckingForAdminAndFlag(player, AdminFlag.Ban))
        {
            menu.AddMenuOption(_baseAdmin.Localizer["menu.lock.ban_player"], (_, _) =>
                PlayersHandle(
                    player, 
                    target => OpenSubsMenus(
                        player,
                        target,
                        baseConfig.BanReasons, 
                        (admin, controller, time, reason) =>
                        Task.Run(() => _baseAdmin.AddBanAsync(admin, controller, time, reason)))));
        }

        if (_baseAdmin.CheckingForAdminAndFlag(player, AdminFlag.Generic))
        {
            menu.AddMenuOption(_baseAdmin.Localizer["menu.lock.mute_player"], (_, _) =>
                PlayersHandle(
                    player, 
                    target => OpenSubsMenus(
                        player, 
                        target, 
                        baseConfig.MuteReasons,
                    (admin, controller, time, reason) =>
                        Task.Run(() => _baseAdmin.AddMuteAsync(admin, controller, time, reason)))));
        }

        menu.Open(player);
        MenuService.Menus.TryAdd(MenuItem.LockControl, menu);
    }

    private void OpenSubsMenus(CCSPlayerController admin, CCSPlayerController target, IEnumerable<string> reasons,
        Action<CCSPlayerController, CCSPlayerController, int, string> handler)
    {
        var reasonMenu = _menuService.CreateMenu(_baseAdmin.Localizer["menu.lock_control"]);
        foreach (var reason in reasons)
        {
            reasonMenu.AddMenuOption(reason, (_, _) =>
            {
                var timesMenu = _menuService.CreateMenu(_baseAdmin.Localizer["menu.lock_control"]);

                foreach (var (key, value) in _baseAdmin.BaseConfig.Times)
                {
                    timesMenu.AddMenuOption(key, (_, _) => { handler(admin, target, value, reason); });
                }

                timesMenu.Open(admin);
            });
        }

        reasonMenu.Open(admin);
    }
}