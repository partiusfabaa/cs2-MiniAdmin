using System;
using System.Collections.Generic;
using BaseAdminApi.Enums;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace BaseAdmin.Menu;

public interface IMenuService
{
    IMenu CreateMenu(string title);
    void AddMenuOptions(string display, Action<CCSPlayerController, ChatMenuOption> handler, bool disabled);
    void AddMenuOptions(MenuItem type, string display, Action<CCSPlayerController, ChatMenuOption> handler, bool disabled);
}