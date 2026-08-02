using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang.Internal;

/// <summary>
/// The one place an entry point's reflected facts are read.
/// </summary>
/// <remarks>
/// <see cref="SlangProgram"/> reads its entry points in the constructor and
/// <see cref="SlangReflection"/> reads them again — from the same layout, for
/// the same indices. Two readers of one fact is one reader too many: the two
/// lists index the same entry points and are handed to callers as
/// interchangeable, so a member added to one and not the other is a silent
/// disagreement rather than a compile error.
/// </remarks>
internal static unsafe class SlangEntryPoints
{
    /// <summary>
    /// Reads name, stage and thread-group size for one entry point.
    /// </summary>
    public static SlangEntryPointInfo Read(SlangEntryPointLayout* entryPoint)
    {
        string name = SlangUtf8.ToString(SlangApi.spReflectionEntryPoint_getName(entryPoint)) ?? string.Empty;
        ShaderStages stage = SlangStages.ToShaderStages(SlangApi.spReflectionEntryPoint_getStage(entryPoint));

        // SlangUInt is 64-bit; the call writes axisCount entries and has no
        // result code. Measured on v2026.14.1 / win-x64: a compute entry point
        // declared [numthreads(8, 4, 1)] reports (8, 4, 1) and every
        // non-compute stage reports (1, 1, 1) — not zeroes, so there is
        // nothing to normalize.
        ulong* sizes = stackalloc ulong[3];

        sizes[0] = 0;
        sizes[1] = 0;
        sizes[2] = 0;

        SlangApi.spReflectionEntryPoint_getComputeThreadGroupSize(entryPoint, 3, sizes);

        return new SlangEntryPointInfo(name, stage, (uint)sizes[0], (uint)sizes[1], (uint)sizes[2]);
    }
}
