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

        if (_baseAdmin.CheckingForAdminAndFlag(player, AdminFlag.Ban))
        {
            menu.AddMenuOption(_baseAdmin.Localizer["menu.lock.ban_player"], null!, true);
        }

        if (_baseAdmin.CheckingForAdminAndFlag(player, AdminFlag.Generic))
        {
            menu.AddMenuOption(_baseAdmin.Localizer["menu.lock.mute_player"], null!, true);
        }

        _menuService.OpenMenu(player, menu);
        MenuService.Menus.TryAdd(MenuItem.LockControl, menu);
    }
}