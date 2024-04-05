using BaseAdminApi.Enums;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace BaseAdmin.Menu.Menus;

public class PlayersControlMenu : AdminMenuBase
{
    private readonly BaseAdmin _baseAdmin;
    private readonly MenuService _menuService;

    public PlayersControlMenu(BaseAdmin baseAdmin, MenuService menuService) : base(baseAdmin, menuService)
    {
        _baseAdmin = baseAdmin;
        _menuService = menuService;
    }

    public override void Handle(CCSPlayerController player, ChatMenuOption option)
    {
        var menu = _menuService.CreateMenu(_baseAdmin.Localizer["menu.players_control"]);
        if (_baseAdmin.CheckingForAdminAndFlag(player, AdminFlag.Kick))
        {
            menu.AddMenuOption(_baseAdmin.Localizer["menu.players.kick_player"], (_, _) => PlayersHandle(player, controller =>
            {
                controller.Kick("Kick");
                _baseAdmin.PrintToChatAll(_baseAdmin.Localizer["player.kick", player.PlayerName, controller.PlayerName]);
            }));
        }

        if (_baseAdmin.CheckingForAdminAndFlag(player, AdminFlag.Slay))
        {
            menu.AddMenuOption(_baseAdmin.Localizer["menu.players.kill_player"], (_, _) => PlayersHandle(player, controller =>
            {
                controller.PlayerPawn.Value?.CommitSuicide(true, true); 
                _baseAdmin.PrintToChatAll(_baseAdmin.Localizer["player.kill", player.PlayerName, controller.PlayerName]);
            }));
        }

        menu.Open(player);
        MenuService.Menus.TryAdd(MenuItem.PlayerControl, menu);
    }
}