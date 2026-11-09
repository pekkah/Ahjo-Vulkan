using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan.Samples.HelloFrameGen;

/// <summary>The two presentation paths this sample can run.</summary>
internal enum FrameGenMode
{
    /// <summary>DLSS Frame Generation: two presents per rendered frame (#253 spec D5).</summary>
    Fg,

    /// <summary>The control: the same scene, UI and composite, one present of
    /// <c>Backbuffer</c> per rendered frame.</summary>
    Off,
}

/// <summary>How motion vectors are handed to <c>DLSSG.MvecScale</c>. Diagnostic,
/// for #253 PR C step 19b only.</summary>
internal enum MvecScaleMode
{
    /// <summary><c>(1, 1)</c> over pixel motion vectors: the DLSS-FG guide's reading.</summary>
    Pixels,

    /// <summary><c>(1/RenderWidth, 1/RenderHeight)</c> over the same pixel
    /// vectors: the header's "normalize to [-1,1]" reading.</summary>
    Normalized,
}

/// <summary>
/// The parsed command line. Hand-rolled: no third-party parser and no
/// reflection, because the wrapper's AOT contract runs all the way down to the
/// samples.
/// </summary>
internal readonly record struct FrameGenOptions
{
    public FrameGenMode     Mode              { get; init; }
    public VkPresentModeKHR PresentMode       { get; init; }
    public ulong            MaxFrames         { get; init; }
    public int              ThrottleMs        { get; init; }
    public string?          CaptureGenerated  { get; init; }
    public string?          CaptureReal       { get; init; }
    public bool             Freeze            { get; init; }
    public bool             RequireFg         { get; init; }
    public bool             Validation        { get; init; }
    public LatencySleepMode Reflex            { get; init; }
    public string           ReflexName        { get; init; }
    public ulong            LatencyReport     { get; init; }
    public uint             ReflexIntervalUs  { get; init; }
    public bool             ReflexIntervalFrame { get; init; }
    public bool             ReflexIntervalGiven { get; init; }
    public bool             DiagnosticFgWithoutReflex { get; init; }
    public bool             SkipSleep         { get; init; }
    public bool             Borderless        { get; init; }
    public bool             TransposeMatrices { get; init; }
    public MvecScaleMode    MvecScale         { get; init; }

    public bool Captures => CaptureGenerated is not null || CaptureReal is not null;

    public const string Usage =
        "HelloFrameGen [--mode fg|off] [--present fifo|immediate] [--frames N]\n" +
        "              [--throttle-ms N]\n" +
        "              [--capture-generated <png>] [--capture-real <png>]\n" +
        "              [--freeze] [--require-fg] [--no-validation]\n" +
        "              [--reflex off|on|boost] [--latency-report N] [--borderless]\n" +
        "              [--reflex-interval-us N|frame]\n" +
        "  diagnostics (#253 PR C step 19): [--transpose-matrices] [--mvec-scale pixels|normalized]\n" +
        "                                   [--diagnostic-fg-without-reflex] [--skip-sleep]";

    public static bool TryParse(string[] args, out FrameGenOptions options, out string? error)
    {
        FrameGenMode     mode        = FrameGenMode.Fg;
        VkPresentModeKHR present     = VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR;
        ulong            maxFrames   = 0;
        bool             framesGiven = false;
        int              throttleMs  = 0;
        string?          capGen      = null;
        string?          capReal     = null;
        bool             freeze      = false;
        bool             requireFg   = false;
        bool             validation  = true;
        // Reflex Low Latency defaults to On: NVIDIA's required default
        // (Reflex guide l. 1017; docs/reflex-notes.md §5).
        LatencySleepMode reflex      = LatencySleepMode.On;
        string           reflexName  = "on";
        ulong            latency     = 0;
        uint             intervalUs  = 0;
        bool             intervalFrame = false;
        bool             intervalGiven = false;
        bool             fgNoReflex  = false;
        bool             skipSleep   = false;
        bool             borderless  = false;
        bool             transpose   = false;
        MvecScaleMode    mvecScale   = MvecScaleMode.Pixels;

        options = default;
        for (int i = 0; i < args.Length; i++)
        {
            string  a = args[i];
            string? v = i + 1 < args.Length ? args[i + 1] : null;
            switch (a)
            {
                case "--mode":
                    if (v is "fg") mode = FrameGenMode.Fg;
                    else if (v is "off") mode = FrameGenMode.Off;
                    else { error = "--mode needs a value: fg or off."; return false; }
                    i++;
                    break;

                case "--present":
                    if (v is "fifo") present = VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR;
                    else if (v is "immediate") present = VkPresentModeKHR.VK_PRESENT_MODE_IMMEDIATE_KHR;
                    else { error = "--present needs a value: fifo or immediate."; return false; }
                    i++;
                    break;

                case "--frames":
                    if (v is null || !ulong.TryParse(v, out maxFrames) || maxFrames == 0)
                    {
                        error = "--frames needs a positive integer.";
                        return false;
                    }
                    framesGiven = true;
                    i++;
                    break;

                case "--throttle-ms":
                    if (v is null || !int.TryParse(v, out throttleMs) || throttleMs < 0)
                    {
                        error = "--throttle-ms needs a non-negative integer.";
                        return false;
                    }
                    i++;
                    break;

                case "--capture-generated":
                    if (v is null) { error = "--capture-generated needs a file path."; return false; }
                    capGen = v;
                    i++;
                    break;

                case "--capture-real":
                    if (v is null) { error = "--capture-real needs a file path."; return false; }
                    capReal = v;
                    i++;
                    break;

                case "--reflex":
                    switch (v)
                    {
                        case "off":   reflex = LatencySleepMode.Off;         break;
                        case "on":    reflex = LatencySleepMode.On;          break;
                        case "boost": reflex = LatencySleepMode.OnPlusBoost; break;
                        default: error = "--reflex needs a value: off, on or boost."; return false;
                    }
                    reflexName = v;
                    i++;
                    break;

                case "--latency-report":
                    if (v is null || !ulong.TryParse(v, out latency) || latency == 0)
                    {
                        error = "--latency-report needs a positive frame interval, e.g. 120.";
                        return false;
                    }
                    i++;
                    break;

                case "--reflex-interval-us":
                    if (v is "frame") intervalFrame = true;
                    else if (v is null || !uint.TryParse(v, out intervalUs))
                    {
                        error = "--reflex-interval-us needs microseconds or 'frame'.";
                        return false;
                    }
                    intervalGiven = true;
                    i++;
                    break;

                case "--mvec-scale":
                    if (v is "pixels") mvecScale = MvecScaleMode.Pixels;
                    else if (v is "normalized") mvecScale = MvecScaleMode.Normalized;
                    else { error = "--mvec-scale needs a value: pixels or normalized."; return false; }
                    i++;
                    break;

                case "--freeze":             freeze     = true;  break;
                case "--require-fg":         requireFg  = true;  break;
                case "--no-validation":      validation = false; break;
                case "--diagnostic-fg-without-reflex": fgNoReflex = true; break;
                case "--skip-sleep":         skipSleep  = true;  break;
                case "--borderless":         borderless = true;  break;
                case "--transpose-matrices": transpose  = true;  break;

                default:
                    error = $"Unknown argument '{a}'.\n{Usage}";
                    return false;
            }
        }

        // Reflex may not be turned off while Frame Generation is on: "Users
        // should not be able to disable NVIDIA Reflex Low Latency when Frame
        // Generation is ON" (RTX UI Developer Guidelines, UI l. 149-150; #253
        // spec F1). --mode off --reflex off stays legal: it is the latency
        // baseline.
        if (mode == FrameGenMode.Fg && reflexName == "off" && !fgNoReflex)
        {
            error = "--mode fg --reflex off is not allowed. NVIDIA's RTX UI Developer Guidelines: \"DLSS Frame " +
                    "Generation requires NVIDIA Reflex Low Latency integration and enablement. Users should not be " +
                    "able to disable NVIDIA Reflex Low Latency when Frame Generation is ON\" (UI l. 149-150). Use " +
                    "--reflex on or --reflex boost, or --mode off --reflex off for the latency baseline.";
            return false;
        }

        // A capture run without --frames would run forever and never capture,
        // so it gets a default bound: frame 300 (#253 PR C step 19f).
        if (!framesGiven)
            maxFrames = capGen is null && capReal is null ? ulong.MaxValue : 300;

        options = new FrameGenOptions
        {
            Mode              = mode,
            PresentMode       = present,
            MaxFrames         = maxFrames,
            ThrottleMs        = throttleMs,
            CaptureGenerated  = capGen,
            CaptureReal       = capReal,
            Freeze            = freeze,
            RequireFg         = requireFg,
            Validation        = validation,
            Reflex            = reflex,
            ReflexName        = reflexName,
            LatencyReport     = latency,
            ReflexIntervalUs  = intervalUs,
            ReflexIntervalFrame = intervalFrame,
            ReflexIntervalGiven = intervalGiven,
            DiagnosticFgWithoutReflex = fgNoReflex,
            SkipSleep         = skipSleep,
            Borderless        = borderless,
            TransposeMatrices = transpose,
            MvecScale         = mvecScale,
        };
        error = null;
        return true;
    }
}
