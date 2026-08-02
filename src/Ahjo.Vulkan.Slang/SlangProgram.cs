using System.Text;

using Ahjo.Vulkan.Slang.Internal;
using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang;

/// <summary>
/// A linked Slang program: the one object that can produce SPIR-V, and the
/// layout that SPIR-V was compiled against.
/// </summary>
/// <remarks>
/// <para>There is deliberately no way to construct one of these from anything
/// but a successful <c>IComponentType::link</c>. Composition changes the
/// layout: the same module reflected alone and reflected inside a composite
/// reports different descriptor sets and binding indices
/// (<c>slang.h:5378-5386</c>). Binding the type so that the SPIR-V a caller
/// fetches and the layout a caller reads come from the same linked object is
/// the cheapest place to make that impossible to get wrong.</para>
/// <para><b>A linked program is not necessarily a compilable one.</b> A program
/// whose global scope holds an interface-typed parameter links, reflects and
/// reports its entry points correctly, and refuses only at
/// <see cref="Spirv"/> — with <c>error[E50100]: no type conformances found</c>,
/// until an implementation is named through
/// <see cref="SlangCompileRequest.TypeConformances"/> or
/// <see cref="SlangProgramBuilder.AddTypeConformance"/>.</para>
/// <para>Dispose this before the <see cref="SlangSession"/> it came from.</para>
/// </remarks>
public sealed unsafe class SlangProgram : IDisposable
{
    private readonly SlangEntryPointInfo[] _entryPoints;
    private readonly nint[] _codeBlobs;
    private IComponentType* _linked;
    private string? _warnings;
    private SlangReflection? _programStageUnionReflection;
    private SlangReflection? _perEntryPointUsageReflection;

    internal SlangProgram(IComponentType* linked, string? warnings)
    {
        _linked = linked;
        _warnings = warnings;

        try
        {
            _entryPoints = ReadEntryPoints(linked);
            _codeBlobs = new nint[_entryPoints.Length];
            SpecializationParameterCount = (int)linked->getSpecializationParamCount();
        }
        catch
        {
            linked->release();
            _linked = null;

            throw;
        }
    }

    /// <summary>Number of entry points linked into this program.</summary>
    public int EntryPointCount => _entryPoints.Length;

    /// <summary>
    /// Number of specialization parameters the linked program still has —
    /// <c>IComponentType::getSpecializationParamCount()</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>A report, not a predicate.</b> It does <em>not</em> say whether
    /// <see cref="Spirv"/> will succeed. Measured on <c>v2026.14.1</c> /
    /// win-x64:</para>
    /// <list type="table">
    /// <item><description>a concrete program (<c>ConstantBuffer</c>s and
    /// <c>ParameterBlock</c>s) — <c>0</c></description></item>
    /// <item><description>a <c>ParameterBlock&lt;ISurface&gt;</c> with no type
    /// conformance — <c>1</c></description></item>
    /// <item><description>the same program with
    /// <c>AddTypeConformance("Glossy", "ISurface")</c> — <b><c>1</c></b>, and it
    /// generates code: a conformance does not consume a specialization
    /// parameter</description></item>
    /// <item><description>an <c>interface</c> declared but dispatched statically
    /// — <c>0</c></description></item>
    /// </list>
    /// <para>So a non-zero value means "this program has an unresolved
    /// existential or generic parameter", which is worth reporting; the third
    /// row is why nothing in this package refuses a program on the strength of
    /// it.</para>
    /// </remarks>
    public int SpecializationParameterCount { get; }

    /// <summary>
    /// Diagnostics Slang produced on calls that nonetheless succeeded —
    /// warnings, and any note the backend emitted while generating code.
    /// <see langword="null"/> when nothing was reported.
    /// </summary>
    /// <remarks>
    /// Grows as code is generated: <see cref="Spirv"/> appends whatever the
    /// backend said about the entry point it compiled. A failure never lands
    /// here — it throws <see cref="SlangCompilationException"/>.
    /// </remarks>
    public string? Warnings => _warnings;

    /// <summary>
    /// This program's binding surface, with every
    /// <c>DescriptorBinding.Stages</c> set to the union of the program's
    /// entry-point stages.
    /// </summary>
    /// <remarks>
    /// Shorthand for
    /// <see cref="GetReflection"/><c>(SlangStageAttribution.ProgramStageUnion)</c>.
    /// Built once and cached; the reflection is a view of this program and does
    /// not outlive it.
    /// </remarks>
    public SlangReflection Reflection => GetReflection(SlangStageAttribution.ProgramStageUnion);

    internal IComponentType* LinkedComponent
        => _linked != null ? _linked : throw new ObjectDisposedException(nameof(SlangProgram));

    /// <summary>
    /// This program's binding surface as <c>DescriptorBinding</c>,
    /// <c>PushConstantRange</c> and <c>VertexAttributeDescription</c> values.
    /// </summary>
    /// <remarks>
    /// <para>The layout is read from the same linked component
    /// <see cref="Spirv"/> generates from, so the pipeline layout a caller
    /// builds and the SPIR-V a caller loads cannot disagree.</para>
    /// <para>Each mode is built once and cached. See
    /// <see cref="SlangStageAttribution"/> for what
    /// <see cref="SlangStageAttribution.PerEntryPointUsage"/> costs — it
    /// compiles every entry point and can therefore throw.</para>
    /// </remarks>
    /// <exception cref="SlangCompilationException">
    /// Slang refused to produce the program layout, or — in
    /// <see cref="SlangStageAttribution.PerEntryPointUsage"/> mode — refused to
    /// generate metadata for an entry point.
    /// </exception>
    public SlangReflection GetReflection(SlangStageAttribution attribution)
    {
        _ = LinkedComponent;

        return attribution switch
        {
            SlangStageAttribution.ProgramStageUnion
                => _programStageUnionReflection ??= new SlangReflection(this, attribution),
            SlangStageAttribution.PerEntryPointUsage
                => _perEntryPointUsageReflection ??= new SlangReflection(this, attribution),
            _ => throw new ArgumentOutOfRangeException(nameof(attribution), attribution, "Not a SlangStageAttribution value."),
        };
    }

    /// <summary>
    /// The <paramref name="index"/>-th entry point's name, stage and thread
    /// group size.
    /// </summary>
    /// <remarks>
    /// The index is the same one <see cref="Spirv"/> takes: entry point
    /// <c>i</c>'s reflection and entry point <c>i</c>'s SPIR-V describe the
    /// same function. Both follow the order the components were composed in.
    /// </remarks>
    public SlangEntryPointInfo EntryPoint(int index)
    {
        _ = LinkedComponent;
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _entryPoints.Length);

        return _entryPoints[index];
    }

    /// <summary>
    /// SPIR-V words for entry point <paramref name="entryPointIndex"/>, ready
    /// for <c>Device.CreateShaderModule(ReadOnlySpan&lt;uint&gt;)</c>.
    /// </summary>
    /// <remarks>
    /// <para>The span is a view over Slang-owned native memory this program
    /// holds a reference on — <b>valid until <see cref="Dispose"/></b>, the
    /// same contract <c>SpirvBlob.Words</c> states
    /// (<c>src/Ahjo.Vulkan/Memory/SpirvBlob.cs:37-47</c>). Copy it if it has
    /// to outlive the program.</para>
    /// <para>Code is generated once per index and cached, so repeated calls
    /// return the same span.</para>
    /// <para><b>A linked program is not necessarily a compilable one.</b> An
    /// interface-typed parameter with no type conformance in the linkage links,
    /// reflects and reports its entry points, and refuses only here — see the
    /// type's remarks.</para>
    /// </remarks>
    /// <exception cref="SlangCompilationException">
    /// The backend refused to generate code for this entry point. There is no
    /// path on which this returns an empty span instead.
    /// </exception>
    public ReadOnlySpan<uint> Spirv(int entryPointIndex)
    {
        IComponentType* linked = LinkedComponent;

        ArgumentOutOfRangeException.ThrowIfNegative(entryPointIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(entryPointIndex, _entryPoints.Length);

        var blob = (ISlangBlob*)_codeBlobs[entryPointIndex];

        if (blob == null)
        {
            ISlangBlob* code = null;
            ISlangBlob* diagnostics = null;
            int rc = linked->getEntryPointCode(entryPointIndex, 0, &code, &diagnostics);
            string text = SlangUtf8.TakeDiagnostics(&diagnostics);

            if (rc < 0 || code == null)
            {
                // The one failure this package can say more about than Slang
                // does: E50100 means an interface-typed parameter reached code
                // generation with no implementation in the linkage, and the fix
                // is a conformance rather than anything about this entry point.
                throw text.Contains("E50100", StringComparison.Ordinal)
                    ? new SlangCompilationException(
                        "Slang compilation failed: error[E50100]: no type conformances found. Entry point "
                        + $"{entryPointIndex} dispatches through an interface-typed parameter, so at least one "
                        + "implementation must be in the linkage. Add one with "
                        + "SlangCompileRequest.TypeConformances or "
                        + "SlangProgramBuilder.AddTypeConformance(concreteType, interfaceType). This shape links "
                        + "successfully — the failure can only appear here.",
                        text,
                        innerException: null)
                    : new SlangCompilationException(
                        $"getEntryPointCode({entryPointIndex}) (0x{rc:X8})",
                        text);
            }

            nuint size = code->getBufferSize();

            if (size == 0 || size % 4 != 0)
            {
                code->release();

                throw new SlangCompilationException(
                    $"Slang compilation failed: getEntryPointCode({entryPointIndex}) produced {size} bytes, which is not a non-empty multiple of 4.",
                    text,
                    innerException: null);
            }

            _warnings = JoinDiagnostics(_warnings, text);
            _codeBlobs[entryPointIndex] = (nint)code;
            blob = code;
        }

        return new ReadOnlySpan<uint>(blob->getBufferPointer(), (int)(blob->getBufferSize() / 4));
    }

    /// <summary>Releases the linked component and every cached code blob.</summary>
    public void Dispose()
    {
        IComponentType* linked = _linked;

        _linked = null;

        for (int i = 0; i < _codeBlobs.Length; i++)
        {
            var blob = (ISlangBlob*)_codeBlobs[i];

            _codeBlobs[i] = 0;

            if (blob != null)
            {
                blob->release();
            }
        }

        if (linked != null)
        {
            linked->release();
        }
    }

    /// <summary>
    /// Concatenates the non-empty diagnostics texts collected along a compile,
    /// or <see langword="null"/> when they were all empty.
    /// </summary>
    internal static string? JoinDiagnostics(params string?[] parts)
    {
        StringBuilder? builder = null;
        string? single = null;

        foreach (string? part in parts)
        {
            if (string.IsNullOrEmpty(part))
            {
                continue;
            }

            if (single is null && builder is null)
            {
                single = part;

                continue;
            }

            builder ??= new StringBuilder(single);
            single = null;

            if (builder.Length > 0 && builder[^1] != '\n')
            {
                builder.Append('\n');
            }

            builder.Append(part);
        }

        return builder is not null ? builder.ToString() : single;
    }

    private static SlangEntryPointInfo[] ReadEntryPoints(IComponentType* linked)
    {
        ISlangBlob* diagnostics = null;
        var layout = (SlangProgramLayout*)linked->getLayout(0, &diagnostics);
        string text = SlangUtf8.TakeDiagnostics(&diagnostics);

        if (layout == null)
        {
            throw new SlangCompilationException("IComponentType::getLayout", text);
        }

        ulong count = SlangApi.spReflection_getEntryPointCount(layout);
        var infos = new SlangEntryPointInfo[(int)count];

        for (ulong i = 0; i < count; i++)
        {
            infos[(int)i] = SlangEntryPoints.Read(SlangApi.spReflection_getEntryPointByIndex(layout, i));
        }

        return infos;
    }
}
