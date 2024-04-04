using BaseAdminApi;
using BaseAdminApi.Enums;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace BaseAdmin.Commands.Commands;

public class FunCommands
{
    private readonly BaseAdmin _baseAdmin;

    public FunCommands(BaseAdmin baseAdmin)
    {
        _baseAdmin = baseAdmin;
    }
    
    [RegisterCommand("css_god", AdminFlag.Cheats, usage: "[#userid or name]")]
    public void OnCmdGod(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null) return;

        if (command.ArgCount >= 2)
        {
            if (!Utils.GetPlayer(command.GetArg(1), out var target))
            {
                _baseAdmin.ReplyToCommand(controller, _baseAdmin.Localizer["player_not_found"]);
                return;
            }

            var targetPawn = target.PlayerPawn.Value;
            if (targetPawn == null) return;

            targetPawn.TakesDamage ^= true;
            _baseAdmin.PrintToChat(target, _baseAdmin.Localizer["give_god_target"]);
        }
        else
        {
            var playerPawn = controller.PlayerPawn.Value;
            if (playerPawn == null) return;
            
            playerPawn.TakesDamage ^= true;
            _baseAdmin.PrintToChat(controller, _baseAdmin.Localizer["give_god"]);
        }
    }
}