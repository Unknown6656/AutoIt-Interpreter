using Unknown6656.AutoIt3.Runtime.Native;

namespace Unknown6656.AutoIt3.Runtime
{
    public record Metadata(OS SupportedPlatforms, bool IsDeprecated)
    {
        public static Metadata Default { get; } = new();

        public static Metadata Deprecated { get; } = new(OS.Any, true);

        public static Metadata WindowsOnly { get; } = new(OS.Windows, false);

        public static Metadata MacOSOnly { get; } = new(OS.MacOS, false);

        public static Metadata LinuxOnly { get; } = new(OS.Linux, false);

        public static Metadata UnixOnly { get; } = new(OS.UnixLike, false);


        public Metadata()
            : this(OS.Any, false)
        {
        }

        public bool SupportsPlatfrom(OS platform) => SupportedPlatforms.HasFlag(platform);

        public static Metadata operator +(Metadata m1, Metadata m2) => m1 | m2;

        public static Metadata operator |(Metadata m1, Metadata m2) => new(m1.SupportedPlatforms | m2.SupportedPlatforms, m1.IsDeprecated | m2.IsDeprecated);

        public static Metadata operator &(Metadata m1, Metadata m2) => new(m1.SupportedPlatforms & m2.SupportedPlatforms, m1.IsDeprecated & m2.IsDeprecated);
    }
}
