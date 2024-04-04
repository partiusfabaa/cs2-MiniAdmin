using BaseAdminApi.Enums;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace BaseAdmin.Menu.Menus;

public class ServerControlMenu : AdminMenuBase
{
    private readonly BaseAdmin _baseAdmin;
    private readonly MenuService _menuService;

    public ServerControlMenu(BaseAdmin baseAdmin, MenuService menuService) : base(baseAdmin, menuService)
    {
        _baseAdmin = baseAdmin;
        _menuService = menuService;
    }

    public override void Handle(CCSPlayerController player, ChatMenuOption option)
    {
        var menu = _menuService.CreateMenu(_baseAdmin.Localizer["menu.server_control"]);

        menu.AddMenuOption(_baseAdmin.Localizer["menu.server.change_map"], SelectMapMenu);

        _menuService.OpenMenu(player, menu);
        MenuService.Menus.TryAdd(MenuItem.PlayerControl, menu);
    }

    private void SelectMapMenu(CCSPlayerController player, ChatMenuOption option)
    {
        var menu = _menuService.CreateMenu(_baseAdmin.Localizer["menu.server_control"]);

        foreach (var map in _baseAdmin.BaseConfig.Maps)
        {
            var mapName = map.Replace("ws:", "");
            menu.AddMenuOption(mapName, (_, _) =>
            {
                _baseAdmin.PrintToChatAll(_baseAdmin.Localizer["menu.server.map_change", player.PlayerName, mapName]);
                _baseAdmin.ChangeMap(map, map.StartsWith("ws:"));
            });
        }
        _menuService.OpenMenu(player, menu);
    }
}