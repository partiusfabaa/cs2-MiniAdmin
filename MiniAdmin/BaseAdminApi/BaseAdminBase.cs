// using CounterStrikeSharp.API.Core;
// using CounterStrikeSharp.API.Modules.Commands;
//
// namespace BaseAdminApi;
//
// public interface IBaseAdminBase
// {
//     IBaseAdminApi Api { get; set; }
// }
//
// public abstract class BaseAdminBase : IBaseAdminBase
// {
//     public IBaseAdminApi Api { get; set; }
//
//     protected BaseAdminBase(IBaseAdminApi api)
//     {
//         Api = api;
//     }
//
//     public void BanPlayer(CCSPlayerController? admin, CCSPlayerController target, int time, string reason) =>
//         Api.BanPlayer(admin, target, time, reason);
//
//     public async Task BanPlayerAsync(CCSPlayerController? admin, CCSPlayerController target, int time, string reason) =>
//         await Api.BanPlayerAsync(admin, target, time, reason);
//
//     public void UnBanPlayer(CCSPlayerController? admin, string steamId, string reason) =>
//         Api.UnBanPlayer(admin, steamId, reason);
//
//     public async Task UnBanPlayerAsync(CCSPlayerController? admin, string steamId, string reason) =>
//         await Api.UnBanPlayerAsync(admin, steamId, reason);
//
//     public bool CheckingForAdminAndFlag(CCSPlayerController? player, AdminFlag flag) =>
//         Api.CheckingForAdminAndFlag(player, flag);
//
//     public void ReplyToCommand(CCSPlayerController? controller, string message, params object?[] args) =>
//         Api.ReplyToCommand(controller, message, args);
//
//     public void RegisterCommand(string command, AdminFlag flag, int args, string usage,
//         Action<CCSPlayerController?, CommandInfo> handler) => Api.RegisterCommand(command, flag, args, usage, handler);
//     
//     public void RegisterCommand(string command, AdminFlag flag, Action<CCSPlayerController?, CommandInfo> handler) =>
//         RegisterCommand(command, flag, 0, string.Empty, handler);
// }