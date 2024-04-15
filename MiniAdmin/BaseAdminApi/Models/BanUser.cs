namespace BaseAdminApi.Models;

public class BanUser
{
    public required string admin_username { get; set; }
    public required string admin_steamid { get; set; }
    public required string username { get; set; }
    public ulong steamid64 { get; set; }
    public required string steamid { get; set; }
    public required string reason { get; set; }
    public string unban_reason { get; set; } = string.Empty;
    public string admin_unlocked_username { get; set; } = string.Empty;
    public string admin_unlocked_steamid { get; set; } = string.Empty;
    public int start_ban_time { get; set; }
    public int end_ban_time { get; set; }
    public bool ban_active { get; set; }
}