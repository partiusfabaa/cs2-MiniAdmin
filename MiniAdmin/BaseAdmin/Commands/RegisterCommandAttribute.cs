using System;
using BaseAdminApi;
using BaseAdminApi.Enums;

namespace BaseAdmin.Commands;

[AttributeUsage(AttributeTargets.Method)]
public class RegisterCommandAttribute : Attribute
{
    public string Command { get; }
    public string Description { get; }
    public AdminFlag Flag { get; }
    public string Usage { get; }
    public int MinArgs { get; }
    
    public RegisterCommandAttribute(string command, string description = "", AdminFlag flag = AdminFlag.Ban, int minArgs = 0, string usage = "")
    {
        Command = command;
        Description = description;
        Flag = flag;
        Usage = usage;
        MinArgs = minArgs;
    }
    
    public RegisterCommandAttribute(string command, AdminFlag flag = AdminFlag.Ban, int minArgs = 0, string usage = "")
    {
        Command = command;
        Description = "empty";
        Flag = flag;
        Usage = usage;
        MinArgs = minArgs;
    }
}