namespace BaseAdminApi.Models;

public class Admin
{
    public required string username { get; set; }
    public required string steamid { get; set; }
    public int start_time { get; set; }
    public int end_time { get; set; }
    public int immunity { get; set; }
    public required string flags { get; set; }
}