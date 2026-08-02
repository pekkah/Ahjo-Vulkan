namespace Ahjo.Vulkan.Slang;

/// <summary>
/// A linked program's entry point: the name Slang reports it under, the
/// pipeline stage it runs at, and its compute thread-group size.
/// </summary>
/// <remarks>
/// <para><b><see cref="Name"/> is not the name in the emitted SPIR-V.</b>
/// Measured on <c>v2026.14.1</c> / win-x64, for every fixture in the suite and
/// every stage: reflection reports the Slang function name (<c>vertexMain</c>,
/// <c>computeMain</c>) while the module's <c>OpEntryPoint</c> names it
/// <c>main</c>. <c>VkPipelineShaderStageCreateInfo.pName</c> has to match the
/// SPIR-V, so a caller passing this value there gets
/// <c>VUID-VkPipelineShaderStageCreateInfo-pName-00707</c>. Use <c>"main"</c>,
/// or read the name out of the module.</para>
/// <para><c>spReflectionEntryPoint_getNameOverride</c> does not help: it
/// returned exactly the same string as <c>getName</c> for every fixture, so
/// there is no member here carrying it.</para>
/// </remarks>
/// <param name="Name">
/// Entry-point name as reflection reports it — the Slang function name. See the
/// type's remarks before using it as <c>pName</c>.
/// </param>
/// <param name="Stage">
/// The stage, mapped from Slang's <c>SlangStage</c>. Feeds
/// <c>ShaderStages</c>-shaped wrapper APIs directly.
/// </param>
/// <param name="ThreadGroupSizeX">
/// The <c>[numthreads]</c> X extent. Measured: every non-compute stage reports
/// <c>1, 1, 1</c> rather than zeroes, so this is safe to read unconditionally —
/// but only a compute stage's value means anything.
/// </param>
/// <param name="ThreadGroupSizeY">The <c>[numthreads]</c> Y extent.</param>
/// <param name="ThreadGroupSizeZ">The <c>[numthreads]</c> Z extent.</param>
public readonly record struct SlangEntryPointInfo(
    string Name,
    ShaderStages Stage,
    uint ThreadGroupSizeX,
    uint ThreadGroupSizeY,
    uint ThreadGroupSizeZ);
