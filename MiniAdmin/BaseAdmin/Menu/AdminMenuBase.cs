using System;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace BaseAdmin.Menu;

public abstract class AdminMenuBase : IMenuHandler
{
    private readonly BaseAdmin _baseAdmin;
    private readonly MenuService _menuService;

    public AdminMenuBase(BaseAdmin baseAdmin, MenuService menuService)
    {
        _baseAdmin = baseAdmin;
        _menuService = menuService;
    }

    public abstract void Handle(CCSPlayerController player, ChatMenuOption option);
    
    public void PlayersHandle(CCSPlayerController player, Action<CCSPlayerController> handler)
    {
        var menu = _menuService.CreateMenu(_baseAdmin.Localizer["menu.all_players"]);
        menu.AddMenuOption(_baseAdmin.Localizer["menu.pick_player"], null!, true);
        foreach (var players in Utilities.GetPlayers().Where(u => u.IsValid))
        {
            if (!_baseAdmin.PlayerImmunityComparison(player, players)) continue;
            
            menu.AddMenuOption($"{players.PlayerName} [{players.UserId}]", (_, _) => handler(players));
        }
        
        menu.Open(player);
    }
}