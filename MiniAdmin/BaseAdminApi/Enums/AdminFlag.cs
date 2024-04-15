using System;

namespace BaseAdminApi.Enums;

[Flags]
public enum AdminFlag
{
    Reservation = 'a',
    Generic = 'b',
    Kick = 'c',
    Ban = 'd',
    Unban = 'e',
    Slay = 'f',
    Changemap = 'g',
    Cvar = 'h',
    Config = 'i',
    Chat = 'j',
    Vote = 'k',
    Password = 'l',
    Rcon = 'm',
    Cheats = 'n',
    Vip = 'o',
    Root = 'z'
}