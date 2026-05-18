namespace LaserCursorAppPro.Models;

public class LaserProfile
{
    public string       Name     { get; set; } = "Default";
    public LaserSettings Settings { get; set; } = new();
}
