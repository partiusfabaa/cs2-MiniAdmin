using System;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace BaseAdmin.Menu;

public interface IMenuHandler
{
    void Handle(CCSPlayerController player, ChatMenuOption option);
    void PlayersHandle(CCSPlayerController player, Action<CCSPlayerController> handler);
}
