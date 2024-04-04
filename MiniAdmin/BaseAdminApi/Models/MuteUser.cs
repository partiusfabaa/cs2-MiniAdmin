namespace BaseAdminApi.Models;

public class MuteUser
{
    public int mute_type { get; set; }
    public required string admin_username { get; set; }
    public required string admin_steamid { get; set; }
    public required string username { get; set; }
    public ulong steamid64 { get; set; }
    public required string steamid { get; set; }
    public required string reason { get; set; }
    public required string unmute_reason { get; set; }
    public required string admin_unlocked_username { get; set; }
    public required string admin_unlocked_steamid { get; set; }
    public int start_mute_time { get; set; }
    public int end_mute_time { get; set; }
    public bool mute_active { get; set; }
}