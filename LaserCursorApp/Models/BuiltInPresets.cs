namespace LaserCursorApp.Models;

public static class BuiltInPresets
{
    public static IReadOnlyList<LaserProfile> All { get; } = new[]
    {
        new LaserProfile
        {
            Name     = "Classic Red",
            Settings = new LaserSettings()
        },
        new LaserProfile
        {
            Name = "Blue Ice",
            Settings = new LaserSettings
            {
                DotColor        = "#FF1E8EFF",
                TailColor       = "#FA1E6FFF",
                DotGlowEnabled  = true,
                DotGlowColor    = "#601E8EFF",
            }
        },
        new LaserProfile
        {
            Name = "Golden",
            Settings = new LaserSettings
            {
                DotColor              = "#FFFFD700",
                TailColor             = "#FFFFA500",
                TailGradientEnabled   = true,
                TailGradientEndColor  = "#FAFF4500",
                DotGlowEnabled        = true,
                DotGlowColor          = "#60FFD700",
            }
        },
        new LaserProfile
        {
            Name = "Neon Green",
            Settings = new LaserSettings
            {
                DotColor       = "#FF39FF14",
                TailColor      = "#FA39FF14",
                DotGlowEnabled = true,
                DotGlowColor   = "#6039FF14",
            }
        },
        new LaserProfile
        {
            Name = "Ghost",
            Settings = new LaserSettings
            {
                DotColor  = "#AAFFFFFF",
                TailColor = "#70FFFFFF",
            }
        },
        new LaserProfile
        {
            Name = "Cyberpunk",
            Settings = new LaserSettings
            {
                DotColor             = "#FFFF0099",
                TailColor            = "#FAFF0099",
                TailGradientEnabled  = true,
                TailGradientEndColor = "#FA00FFFF",
                DotGlowEnabled       = true,
                DotGlowColor         = "#70FF0099",
                TailGlowEnabled      = true,
                TailGlowColor        = "#4000FFFF",
                TailGlowWidth        = 6.0,
            }
        },
    };
}
