using System.Buffers;

using Ahjo.Vulkan.Slang.Internal;
using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang;

/// <summary>
/// Composes a program out of an explicit, ordered list of components —
/// <b>the order components are added is the order Slang assigns descriptor
/// bindings, descriptor spaces and entry-point indices; adding the same
/// components in a different order produces a different, equally valid,
/// incompatible layout.</b>
/// </summary>
/// <remarks>
/// <para>That sentence is a measurement, not a caution. The same five
/// components composed as <c>[common, geometry, material, vs, fs]</c> and as
/// <c>[material, common, geometry, fs, vs]</c> produce different set and
/// binding numbers for every parameter and swap the entry-point indices, and
/// both are what the emitted SPIR-V is decorated with. Composing only the
/// entry points — which links, because an entry point carries its module as a
/// requirement — produces a third assignment again. This is why the component
/// list is a list the caller writes rather than something inferred.</para>
/// <para><see cref="Link"/> may be called more than once; the builder holds no
/// linked state and each call returns an independent <see cref="SlangProgram"/>.
/// Nothing here reflects, and nothing here is on a per-frame path — the
/// allocations are setup-time and deliberate.</para>
/// <para><b>There is no <c>Specialize</c> method, and that is a decision.</b>
/// <c>IComponentType::specialize</c> on a component whose global scope holds an
/// interface-typed <c>ParameterBlock</c> segfaults inside Slang's
/// type-legalization pass — reproduced 3/3 on <c>v2026.14.1</c>, with the
/// crash landing in <c>getTargetCode</c> / <c>getEntryPointCode</c> after both
/// <c>specialize</c> and <c>link</c> have returned success. An API whose
/// failure mode is SIGSEGV cannot ship behind a <c>try</c>.
/// <see cref="AddTypeConformance"/> goes through
/// <c>ISession::createTypeConformanceComponentType</c> instead, which was
/// verified to link and emit valid SPIR-V for that same shader. If a later
/// phase adds <c>Specialize</c>, it needs the pre-flight guard from the design
/// spec's D9 rule 3 — not the bare call.</para>
/// </remarks>
public sealed unsafe class SlangProgramBuilder
{
    /// <summary>
    /// Component counts are single to low double digits in practice; above
    /// this the scratch buffer comes from the pool instead of the stack.
    /// </summary>
    private const int StackComponentLimit = 32;

    private readonly SlangSession _session;
    private readonly List<nint> _components = [];
    private readonly List<(string Concrete, string Interface)> _conformances = [];

    internal SlangProgramBuilder(SlangSession session) => _session = session;

    /// <summary>Appends a module to the component list.</summary>
    /// <remarks>
    /// A module contributes its global-scope parameters. Adding the modules
    /// <em>and</em> the entry points is not the same composite as adding the
    /// entry points alone — see the type's remarks.
    /// </remarks>
    public SlangProgramBuilder Add(SlangModule module)
    {
        ArgumentNullException.ThrowIfNull(module);

        _components.Add((nint)module.Component);

        return this;
    }

    /// <summary>Appends an entry point to the component list.</summary>
    /// <remarks>
    /// Entry points land in <see cref="SlangProgram.EntryPoint(int)"/> and
    /// <see cref="SlangProgram.Spirv(int)"/> in the order they are added here.
    /// </remarks>
    public SlangProgramBuilder Add(SlangEntryPoint entryPoint)
    {
        ArgumentNullException.ThrowIfNull(entryPoint);

        _components.Add((nint)entryPoint.Component);

        return this;
    }

    /// <summary>
    /// Declares that <paramref name="concreteType"/> implements
    /// <paramref name="interfaceType"/>, so a program using the interface
    /// dynamically can generate code for it.
    /// </summary>
    /// <remarks>
    /// <para>Without at least one conformance, code generation for a program
    /// with an interface-typed parameter fails with
    /// <c>error[E50100]: no type conformances found</c>. This is the supported
    /// route for that case; see the type's remarks for why it is not
    /// <c>specialize</c>.</para>
    /// <para><b>Resolution is deferred to <see cref="Link"/>, on purpose.</b>
    /// A type name can only be looked up against a
    /// <c>ShaderReflection</c>, and obtaining one means having a composite
    /// already — so <see cref="Link"/> composes the modules and entry points
    /// first, resolves the names against <em>that</em> composite's layout, and
    /// then composes a second time with the resulting conformance components
    /// appended. A bad type name therefore surfaces from
    /// <see cref="Link"/> and not from here.</para>
    /// <para>Slang assigns the dispatch ID (the native
    /// <c>conformanceIdOverride</c> is passed as <c>-1</c>); this phase does
    /// not expose an override.</para>
    /// </remarks>
    /// <exception cref="ArgumentException">Either name is null or empty.</exception>
    public SlangProgramBuilder AddTypeConformance(string concreteType, string interfaceType)
    {
        ArgumentException.ThrowIfNullOrEmpty(concreteType);
        ArgumentException.ThrowIfNullOrEmpty(interfaceType);

        _conformances.Add((concreteType, interfaceType));

        return this;
    }

    /// <summary>
    /// Composes the accumulated components and links them into a
    /// <see cref="SlangProgram"/>.
    /// </summary>
    /// <remarks>
    /// Independent of any previous call: the builder keeps no linked state, so
    /// linking twice yields two programs the caller disposes separately.
    /// </remarks>
    /// <exception cref="InvalidOperationException">No components were added.</exception>
    /// <exception cref="ArgumentException">
    /// A name passed to <see cref="AddTypeConformance"/> does not name a type
    /// in the composed program.
    /// </exception>
    /// <exception cref="SlangCompilationException">Slang refused to compose or link.</exception>
    public SlangProgram Link()
    {
        if (_components.Count == 0)
        {
            throw new InvalidOperationException(
                "SlangProgramBuilder.Link() needs at least one component. Add the modules and the entry points the program is made of — in the order whose layout you want — before linking.");
        }

        int total = _components.Count + _conformances.Count;

        Span<nint> scratch = stackalloc nint[StackComponentLimit];
        nint[]? rented = null;

        if (total > StackComponentLimit)
        {
            rented = ArrayPool<nint>.Shared.Rent(total);
            scratch = rented;
        }

        try
        {
            Span<nint> components = scratch[..total];

            for (int i = 0; i < _components.Count; i++)
            {
                components[i] = _components[i];
            }

            return _conformances.Count == 0
                ? LinkDirect(components)
                : LinkWithConformances(components);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<nint>.Shared.Return(rented);
            }
        }
    }

    private SlangProgram LinkDirect(ReadOnlySpan<nint> components)
    {
        IComponentType* composite = CreateComposite(components, out string compositeText);

        try
        {
            return LinkComposite(composite, compositeText);
        }
        finally
        {
            composite->release();
        }
    }

    /// <param name="components">
    /// The module/entry-point components in slots
    /// <c>0 .. _components.Count</c>; the conformance slots after them are
    /// filled here.
    /// </param>
    private SlangProgram LinkWithConformances(Span<nint> components)
    {
        ReadOnlySpan<nint> declared = components[.._components.Count];

        // Pass 1 exists only to give the type names something to resolve
        // against: spReflection_FindTypeByName needs a layout, and a layout
        // needs a composite. Its diagnostics still carry into the program —
        // this is the same composition, just performed twice.
        IComponentType* resolutionComposite = CreateComposite(declared, out string firstCompositeText);
        int created = 0;

        try
        {
            SlangProgramLayout* layout = GetLayout(resolutionComposite);

            for (int i = 0; i < _conformances.Count; i++)
            {
                (string concrete, string iface) = _conformances[i];

                components[_components.Count + i] = (nint)CreateConformance(layout, concrete, iface);
                created++;
            }

            IComponentType* composite = CreateComposite(components, out string compositeText);

            try
            {
                return LinkComposite(composite, SlangProgram.JoinDiagnostics(firstCompositeText, compositeText));
            }
            finally
            {
                composite->release();
            }
        }
        finally
        {
            // link() takes its own references on everything it composed, so
            // the conformance components are ours to drop either way.
            for (int i = 0; i < created; i++)
            {
                ((ITypeConformance*)components[_components.Count + i])->release();
            }

            resolutionComposite->release();
        }
    }

    private ITypeConformance* CreateConformance(SlangProgramLayout* layout, string concreteType, string interfaceType)
    {
        TypeReflection* concrete = FindType(layout, concreteType);
        TypeReflection* iface = FindType(layout, interfaceType);

        ITypeConformance* conformance = null;
        ISlangBlob* diagnostics = null;

        // -1: let Slang assign the dispatch ID (slang.h:4620-4623).
        int rc = _session.Handle->createTypeConformanceComponentType(
            concrete, iface, &conformance, -1, &diagnostics);

        string text = SlangUtf8.TakeDiagnostics(&diagnostics);

        if (rc < 0 || conformance == null)
        {
            throw new SlangCompilationException(
                $"createTypeConformanceComponentType('{concreteType}' : '{interfaceType}') (0x{rc:X8})",
                text);
        }

        return conformance;
    }

    private static TypeReflection* FindType(SlangProgramLayout* layout, string name)
    {
        SlangReflectionType* type;

        Span<byte> scratch = stackalloc byte[SlangUtf8.StackScratchBytes];

        using (var utf8 = new SlangUtf8.ScopedUtf8(scratch, name))
        {
            fixed (byte* namePtr = utf8.Bytes)
            {
                type = SlangApi.spReflection_FindTypeByName(layout, (sbyte*)namePtr);
            }
        }

        if (type == null)
        {
            throw new ArgumentException(
                $"'{name}' does not name a type in the composed program. A conformance's concrete and interface types must both be reachable from the modules that were added — check the spelling, and check that the module declaring '{name}' is one of the components.",
                nameof(name));
        }

        return (TypeReflection*)type;
    }

    private static SlangProgramLayout* GetLayout(IComponentType* composite)
    {
        ISlangBlob* diagnostics = null;
        var layout = (SlangProgramLayout*)composite->getLayout(0, &diagnostics);
        string text = SlangUtf8.TakeDiagnostics(&diagnostics);

        if (layout == null)
        {
            throw new SlangCompilationException("IComponentType::getLayout", text);
        }

        return layout;
    }

    private IComponentType* CreateComposite(ReadOnlySpan<nint> components, out string diagnostics)
    {
        IComponentType* composite = null;
        ISlangBlob* blob = null;
        int rc;

        // nint is pointer-sized, so the scratch span is layout-identical to
        // the IComponentType*[] the native call wants.
        fixed (nint* first = components)
        {
            rc = _session.Handle->createCompositeComponentType(
                (IComponentType**)first, components.Length, &composite, &blob);
        }

        diagnostics = SlangUtf8.TakeDiagnostics(&blob);

        if (rc < 0 || composite == null)
        {
            throw new SlangCompilationException(
                $"createCompositeComponentType ({components.Length} components) (0x{rc:X8})",
                diagnostics);
        }

        return composite;
    }

    private static SlangProgram LinkComposite(IComponentType* composite, string? carriedWarnings)
    {
        IComponentType* linked = null;
        ISlangBlob* diagnostics = null;
        int rc = composite->link(&linked, &diagnostics);
        string text = SlangUtf8.TakeDiagnostics(&diagnostics);

        if (rc < 0 || linked == null)
        {
            throw new SlangCompilationException($"link (0x{rc:X8})", text);
        }

        return new SlangProgram(linked, SlangProgram.JoinDiagnostics(carriedWarnings, text));
    }
}
