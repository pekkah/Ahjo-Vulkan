using Ahjo.Vulkan.Slang.Internal;
using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang;

/// <summary>
/// One entry point of a <see cref="SlangModule"/>, ready to be composed into a
/// program. Wraps <c>IEntryPoint</c>.
/// </summary>
/// <remarks>
/// An entry point carries its module as a requirement
/// (<c>slang.h:5337-5339</c>), so a composite built from entry points alone
/// links — but it produces a <em>different</em> binding assignment than one
/// that names the modules explicitly. Name both.
/// </remarks>
public sealed unsafe class SlangEntryPoint : IDisposable
{
    private IEntryPoint* _entryPoint;

    private SlangEntryPoint(IEntryPoint* entryPoint, string name, ShaderStages stage)
    {
        _entryPoint = entryPoint;
        Name = name;
        Stage = stage;
    }

    /// <summary>The entry-point function's name.</summary>
    public string Name { get; }

    /// <summary>
    /// The pipeline stage. For an entry point obtained from
    /// <see cref="SlangModule.DefinedEntryPoint(int)"/> this is the stage the
    /// shader's <c>[shader("…")]</c> attribute declares; for one obtained from
    /// <see cref="SlangModule.FindEntryPoint(string, ShaderStages)"/> it is
    /// the stage the caller asked for, because Slang does not check the two
    /// against each other.
    /// </summary>
    public ShaderStages Stage { get; }

    internal IEntryPoint* Handle
        => _entryPoint != null ? _entryPoint : throw new ObjectDisposedException(nameof(SlangEntryPoint));

    internal IComponentType* Component => (IComponentType*)Handle;

    /// <summary>Drops this wrapper's reference to the entry point.</summary>
    public void Dispose()
    {
        IEntryPoint* entryPoint = _entryPoint;

        _entryPoint = null;

        if (entryPoint != null)
        {
            entryPoint->release();
        }
    }

    /// <summary>
    /// Wraps an entry point whose name and stage the caller already knows.
    /// Takes ownership of the reference <c>findAndCheckEntryPoint</c> added.
    /// </summary>
    internal static SlangEntryPoint FromRequestedStage(IEntryPoint* entryPoint, string name, ShaderStages stage)
        => new(entryPoint, name, stage);

    /// <summary>
    /// Wraps an entry point and reads its declared name and stage back out of
    /// reflection. Takes ownership of the reference
    /// <c>getDefinedEntryPoint</c> added; releases it if reflection fails, so
    /// the caller never has to unwind a half-built wrapper.
    /// </summary>
    /// <remarks>
    /// The stage comes from <c>spReflectionEntryPoint_getStage</c> on the
    /// entry point's own single-entry-point layout — <b>not</b> from
    /// <c>spReflectionVariableLayout_getStage</c>, which returns
    /// <c>SLANG_STAGE_NONE</c> for parameters and would silently produce
    /// <see cref="ShaderStages.None"/>.
    /// </remarks>
    internal static SlangEntryPoint FromReflectedStage(IEntryPoint* entryPoint)
    {
        try
        {
            ISlangBlob* diagnostics = null;
            var layout = (SlangProgramLayout*)entryPoint->getLayout(0, &diagnostics);
            string text = SlangUtf8.TakeDiagnostics(&diagnostics);

            if (layout == null || SlangApi.spReflection_getEntryPointCount(layout) == 0)
            {
                throw new SlangCompilationException("IEntryPoint::getLayout", text);
            }

            SlangEntryPointLayout* reflected = SlangApi.spReflection_getEntryPointByIndex(layout, 0);
            string name = SlangUtf8.ToString(SlangApi.spReflectionEntryPoint_getName(reflected)) ?? string.Empty;
            ShaderStages stage = SlangStages.ToShaderStages(SlangApi.spReflectionEntryPoint_getStage(reflected));

            return new SlangEntryPoint(entryPoint, name, stage);
        }
        catch
        {
            entryPoint->release();

            throw;
        }
    }
}
