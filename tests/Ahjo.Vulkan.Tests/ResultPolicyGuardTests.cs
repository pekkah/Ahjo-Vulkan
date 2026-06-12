using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Ahjo.Vulkan.Tests.Generated;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Machine-checks the <c>ThrowIfFailed</c> contract against vk.xml (issue
/// #117). <c>ResultExtensions.ThrowIfFailed</c> only accepts
/// <c>VK_SUCCESS</c>; using it on a command whose <c>successcodes</c> set
/// carries more than one code turns a spec-defined success (e.g.
/// <c>VK_INCOMPLETE</c> from a <c>count → fill</c> race) into a thrown
/// <see cref="VulkanException"/> — the #97 bug class.
///
/// This test scans the wrapper sources for <c>vk…().ThrowIfFailed()</c> and
/// fails on any call to a multi-success command, naming the site. The set of
/// multi-success commands is generated from vk.xml into
/// <see cref="ResultPolicyData.MultiSuccessCommands"/>, so the contract is
/// derived from the registry rather than from a doc-comment convention.
/// </summary>
public sealed partial class ResultPolicyGuardTests
{
    // Commands that are multi-success in vk.xml but whose extra success
    // code(s) are provably unreachable on every wrapper call site, so the
    // plain VK_SUCCESS check is correct. Each entry needs a justification and
    // is kept honest by Allowlist_HasNoDeadEntries below — if the call site
    // goes away (or stops using ThrowIfFailed), the entry must go too.
    private static readonly IReadOnlyDictionary<string, string> Allowlist =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["vkCreateGraphicsPipelines"] =
                "VK_PIPELINE_COMPILE_REQUIRED is only returned when "
                + "VK_PIPELINE_CREATE_FAIL_ON_PIPELINE_COMPILE_REQUIRED_BIT is set in "
                + "VkGraphicsPipelineCreateInfo.flags; GraphicsPipelineBuilder never sets "
                + "flags (defaults to 0), so VK_SUCCESS is the only reachable success code.",
            ["vkCreateComputePipelines"] =
                "VK_PIPELINE_COMPILE_REQUIRED is only returned when "
                + "VK_PIPELINE_CREATE_FAIL_ON_PIPELINE_COMPILE_REQUIRED_BIT is set in "
                + "VkComputePipelineCreateInfo.flags; ComputePipelineBuilder never sets "
                + "flags (defaults to 0), so VK_SUCCESS is the only reachable success code.",
        };

    // A vk* command invocation. The capture is the command name.
    [GeneratedRegex(@"\b(vk[A-Za-z0-9]+)\s*\(")]
    private static partial Regex CommandCall();

    [GeneratedRegex(@"\.ThrowIfFailed\s*\(")]
    private static partial Regex ThrowIfFailedCall();

    [Fact]
    public void NoMultiSuccessCommand_UsesPlainThrowIfFailed()
    {
        var violations = new List<string>();

        foreach (var file in EnumerateWrapperSources())
        {
            var source = StripComments(File.ReadAllText(file));
            var relative = Path.GetRelativePath(WrapperRoot, file);

            foreach (Match call in CommandCall().Matches(source))
            {
                var command = call.Groups[1].Value;
                if (!ResultPolicyData.MultiSuccessCommands.ContainsKey(command))
                    continue;

                // Attribute the throw to its call: scan the statement this
                // call opens (up to the next ';') for a chained ThrowIfFailed.
                var end = source.IndexOf(';', call.Index);
                if (end < 0) end = source.Length;
                var statement = source[call.Index..end];
                if (!ThrowIfFailedCall().IsMatch(statement))
                    continue;

                if (Allowlist.ContainsKey(command))
                    continue;

                var line = LineNumber(source, call.Index);
                violations.Add($"{relative}:{line} — {command} ({CodesOf(command)})");
            }
        }

        Assert.True(
            violations.Count == 0,
            "ThrowIfFailed() is used on Vulkan commands whose vk.xml successcodes set "
            + "carries more than VK_SUCCESS. These turn spec-defined success codes "
            + "(VK_INCOMPLETE, VK_SUBOPTIMAL_KHR, …) into thrown exceptions (issue #97). "
            + "Use ThrowIfErrored() and branch on the returned VkResult, or add a "
            + "justified entry to the allowlist:\n  " + string.Join("\n  ", violations));
    }

    [Fact]
    public void Allowlist_OnlyContainsMultiSuccessCommands()
    {
        // A typo or a command that became single-success after a headers bump
        // would otherwise sit in the allowlist suppressing nothing.
        foreach (var command in Allowlist.Keys)
        {
            Assert.True(
                ResultPolicyData.MultiSuccessCommands.ContainsKey(command),
                $"Allowlist entry '{command}' is not a multi-success command in the "
                + "generated table; remove it.");
        }
    }

    [Fact]
    public void Allowlist_HasNoDeadEntries()
    {
        // Every allowlisted command must actually be used with ThrowIfFailed
        // somewhere in the wrapper; otherwise the suppression is stale and its
        // justification can no longer be reviewed against a real call site.
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in EnumerateWrapperSources())
        {
            var source = StripComments(File.ReadAllText(file));
            foreach (Match call in CommandCall().Matches(source))
            {
                var command = call.Groups[1].Value;
                if (!Allowlist.ContainsKey(command)) continue;
                var end = source.IndexOf(';', call.Index);
                if (end < 0) end = source.Length;
                if (ThrowIfFailedCall().IsMatch(source[call.Index..end]))
                    used.Add(command);
            }
        }

        var dead = Allowlist.Keys.Where(c => !used.Contains(c)).ToArray();
        Assert.True(
            dead.Length == 0,
            "Allowlist entries no longer match any ThrowIfFailed() call site (stale "
            + "suppression — remove them):\n  " + string.Join("\n  ", dead));
    }

    [Fact]
    public void GeneratedTable_IsPopulatedAndCoversKnownCommands()
    {
        // Guards against a regen that silently emptied the table (which would
        // make the scan above pass vacuously).
        Assert.NotEmpty(ResultPolicyData.MultiSuccessCommands);
        foreach (var command in new[]
                 {
                     "vkEnumeratePhysicalDevices",
                     "vkGetPipelineCacheData",
                     "vkGetPhysicalDeviceSurfaceFormatsKHR",
                     "vkAcquireNextImageKHR",
                     "vkWaitForFences",
                 })
        {
            Assert.True(
                ResultPolicyData.MultiSuccessCommands.ContainsKey(command),
                $"Generated table is missing known multi-success command '{command}'; "
                + "regenerate with `dotnet build src/Ahjo.Vulkan.Native -t:RegenerateResultPolicy`.");
        }
    }

    private static string CodesOf(string command) =>
        string.Join(", ", ResultPolicyData.MultiSuccessCommands[command]);

    private static IEnumerable<string> EnumerateWrapperSources() =>
        Directory.EnumerateFiles(WrapperRoot, "*.cs", SearchOption.AllDirectories)
            .Where(p => !IsBuildOutput(p));

    private static bool IsBuildOutput(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/obj/", StringComparison.Ordinal)
            || normalized.Contains("/bin/", StringComparison.Ordinal);
    }

    private static int LineNumber(string text, int index)
    {
        var line = 1;
        for (var i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n') line++;
        }
        return line;
    }

    // Blanks out // line comments and /* */ block comments (replacing their
    // bodies with spaces so character offsets — and therefore line numbers —
    // are preserved) so a vk* mention in prose can't be mistaken for a call.
    private static string StripComments(string source)
    {
        var sb = new StringBuilder(source.Length);
        var i = 0;
        while (i < source.Length)
        {
            if (i + 1 < source.Length && source[i] == '/' && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n') { sb.Append(' '); i++; }
            }
            else if (i + 1 < source.Length && source[i] == '/' && source[i + 1] == '*')
            {
                while (i < source.Length && !(i + 1 < source.Length && source[i] == '*' && source[i + 1] == '/'))
                {
                    sb.Append(source[i] == '\n' ? '\n' : ' ');
                    i++;
                }
                if (i + 1 < source.Length) { sb.Append("  "); i += 2; }
            }
            else
            {
                sb.Append(source[i]);
                i++;
            }
        }
        return sb.ToString();
    }

    private static string WrapperRoot { get; } = ResolveWrapperRoot();

    private static string ResolveWrapperRoot([CallerFilePath] string thisFile = "")
    {
        // thisFile = <repo>/tests/Ahjo.Vulkan.Tests/ResultPolicyGuardTests.cs
        var testDir = Path.GetDirectoryName(thisFile)!;          // tests/Ahjo.Vulkan.Tests
        var repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", ".."));
        var wrapper = Path.Combine(repoRoot, "src", "Ahjo.Vulkan");
        if (!Directory.Exists(wrapper))
            throw new DirectoryNotFoundException(
                $"Wrapper source root not found at '{wrapper}'. Expected to resolve it "
                + "relative to this test file via [CallerFilePath].");
        return wrapper;
    }
}
