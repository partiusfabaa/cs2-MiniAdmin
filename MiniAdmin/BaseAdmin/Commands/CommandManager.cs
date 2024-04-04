using System;
using System.Reflection;
using BaseAdminApi;
using BaseAdminApi.Enums;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;

namespace BaseAdmin.Commands;

public class CommandManager
{
    private readonly BaseAdmin _baseAdmin;

    public CommandManager(BaseAdmin baseAdmin)
    {
        _baseAdmin = baseAdmin;
    }

    public void RegisterCommand(string command, string description, AdminFlag flag, int args, string usage,
        Action<CCSPlayerController?, CommandInfo> handler)
    {
        _baseAdmin.AddCommand(command, description, (player, info) =>
        {
            if (!_baseAdmin.CheckingForAdminAndFlag(player, flag)) return;
            
            if (info.ArgCount - 1 < args)
            {
                _baseAdmin.ReplyToCommand(player, $"{(player == null ? command : command.Replace("css_", "!"))} {usage}");
                return;
            }
            
            handler(player, info);
        });
    }

    public void RegisterCommand(string command, AdminFlag flag, int args, string usage,
        Action<CCSPlayerController?, CommandInfo> handler)
    {
        RegisterCommand(command, "", flag, args, usage, handler);
    }

    public void RegisterCommands(object obj)
    {
        var methods = obj.GetType().GetMethods();
        foreach (var method in methods)
        {
            var attribute = method.GetCustomAttribute<RegisterCommandAttribute>();
            if (attribute == null) continue;
            
            var command = attribute.Command;
            var description = attribute.Description;
            var flag = attribute.Flag;
            var usage = attribute.Usage;
            var minArgs = attribute.MinArgs;

            var handler = (Action<CCSPlayerController?, CommandInfo>)Delegate.CreateDelegate(typeof(Action<CCSPlayerController?, CommandInfo>), obj, method);
            
            RegisterCommand(command, description, flag, minArgs, usage, handler);
        }
    }
}