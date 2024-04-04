using System;
using System.Collections.Generic;
using System.Linq;
using BaseAdmin.Menu.Menus;
using BaseAdminApi.Enums;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace BaseAdmin.Menu;

public class MenuService : IMenuService
{
    public static readonly Dictionary<MenuItem, IMenu> Menus = new();
    private IMenu _adminMenu = null!;

    private readonly BaseAdmin _baseAdmin;

    public List<ChatMenuOption> MenuOptions => _adminMenu.MenuOptions;

    public MenuService(BaseAdmin baseAdmin)
    {
        _baseAdmin = baseAdmin;
        CreateMenu();
    }

    public IMenu CreateMenu(string title)
    {
        return _baseAdmin.Config.UseCenterHtmlMenu ? new CenterHtmlMenu(title) : new ChatMenu(title);
    }
    
    public void AddMenuOptions(string display, Action<CCSPlayerController, ChatMenuOption> handler, bool disabled)
    {
        _adminMenu.AddMenuOption(display, handler, disabled);
    }

    public void AddMenuOptions(MenuItem type, string display, Action<CCSPlayerController, ChatMenuOption> handler,
        bool disabled)
    {
        if (!Menus.TryGetValue(type, out var menu))
        {
            Console.WriteLine($"Menus {type} is null");
            return;
        }

        Console.WriteLine(menu.Title);
        Console.WriteLine($"Added new item");
        menu.AddMenuOption(display, handler, disabled);
    }

    private void CreateMenu()
    {
        _adminMenu = CreateMenu(_baseAdmin.Localizer["menu_title"]);
        var playersControl = new PlayersControlMenu(_baseAdmin, this);
        _adminMenu.AddMenuOption(_baseAdmin.Localizer["menu.players_control"], playersControl.Handle);

        var serverControl = new ServerControlMenu(_baseAdmin, this);
        _adminMenu.AddMenuOption(_baseAdmin.Localizer["menu.server_control"], serverControl.Handle);

        var lockControl = new LockControlMenu(_baseAdmin, this);
        _adminMenu.AddMenuOption(_baseAdmin.Localizer["menu.lock_control"], lockControl.Handle);
    }

    public void OpenMenu(CCSPlayerController controller, IMenu? menu = null)
    {
        menu ??= _adminMenu;
        if (_baseAdmin.Config.UseCenterHtmlMenu)
            MenuManager.OpenCenterHtmlMenu(_baseAdmin, controller, (CenterHtmlMenu)menu);
        else
            MenuManager.OpenChatMenu(controller, (ChatMenu)menu);
    }
}