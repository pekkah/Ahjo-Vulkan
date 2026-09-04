namespace Ahjo.Vulkan.Samples.HelloDlaa;

/// <summary>
/// The four presentation paths this sample can run. <c>Off</c> and
/// <c>Bilinear</c> exist so <c>Quality</c> has an honest control: comparing
/// DLSS against a native-resolution render flatters nothing; comparing it
/// against the <i>same low-resolution render</i> upscaled naively is what shows
/// the reconstruction (spec D3).
/// </summary>
internal enum DlaaMode
{
    /// <summary>DLSS at native resolution — anti-aliasing, no upscale.</summary>
    Dlaa,

    /// <summary>DLSS MaxQuality — renders smaller, reconstructs to the output extent.</summary>
    Quality,

    /// <summary>No DLSS; render at the output extent and blit 1:1 with NEAREST.</summary>
    Off,

    /// <summary>No DLSS; render at the extent <see cref="Quality"/> would use and blit LINEAR.</summary>
    Bilinear,
}

/// <summary>
/// The parsed command line. Hand-rolled: no third-party parser and no
/// reflection, because the wrapper's AOT contract runs all the way down to the
/// samples.
/// </summary>
internal readonly record struct DlaaOptions
{
    public DlaaMode Mode        { get; init; }
    public ulong    MaxFrames   { get; init; }
    public string?  CapturePath { get; init; }
    public bool     RequireDlss { get; init; }
    public bool     Validation  { get; init; }

    /// <summary>DLSS runs in <c>dlaa</c> and <c>quality</c> only.</summary>
    public bool UsesDlss => Mode is DlaaMode.Dlaa or DlaaMode.Quality;

    /// <summary>
    /// Jitter and the mip bias are applied exactly where DLSS is: they are
    /// inputs to the reconstruction, and applying them without it just blurs.
    /// </summary>
    public bool UsesJitter => UsesDlss;

    public const string Usage =
        "HelloDlaa [--mode dlaa|quality|off|bilinear] [--frames N] [--capture <path.png>]\n" +
        "          [--require-dlss] [--no-validation]";

    public static bool TryParse(string[] args, out DlaaOptions options, out string? error)
    {
        DlaaMode mode        = DlaaMode.Dlaa;
        ulong    maxFrames   = 0;
        bool     framesGiven = false;
        string?  capture     = null;
        bool     requireDlss = false;
        bool     validation  = true;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--mode":
                    if (i + 1 >= args.Length)
                    {
                        options = default;
                        error   = "--mode needs a value: dlaa, quality, off or bilinear.";
                        return false;
                    }
                    string value = args[++i];
                    switch (value)
                    {
                        case "dlaa":     mode = DlaaMode.Dlaa;     break;
                        case "quality":  mode = DlaaMode.Quality;  break;
                        case "off":      mode = DlaaMode.Off;      break;
                        case "bilinear": mode = DlaaMode.Bilinear; break;
                        default:
                            options = default;
                            error   = $"Unknown --mode '{value}'. Accepted: dlaa, quality, off, bilinear.";
                            return false;
                    }
                    break;

                case "--frames":
                    if (i + 1 >= args.Length || !ulong.TryParse(args[i + 1], out maxFrames) || maxFrames == 0)
                    {
                        options = default;
                        error   = "--frames needs a positive integer.";
                        return false;
                    }
                    i++;
                    framesGiven = true;
                    break;

                case "--capture":
                    if (i + 1 >= args.Length)
                    {
                        options = default;
                        error   = "--capture needs a file path.";
                        return false;
                    }
                    capture = args[++i];
                    break;

                case "--require-dlss":
                    requireDlss = true;
                    break;

                case "--no-validation":
                    validation = false;
                    break;

                default:
                    options = default;
                    error   = $"Unknown argument '{args[i]}'.\n{Usage}";
                    return false;
            }
        }

        // --capture without --frames would run forever and never capture, so a
        // capture run gets a default bound. 240 frames is a few seconds of
        // motion — long enough for the temporal history to have converged.
        if (!framesGiven)
            maxFrames = capture is null ? ulong.MaxValue : 240;

        options = new DlaaOptions
        {
            Mode        = mode,
            MaxFrames   = maxFrames,
            CapturePath = capture,
            RequireDlss = requireDlss,
            Validation  = validation,
        };
        error = null;
        return true;
    }
}
