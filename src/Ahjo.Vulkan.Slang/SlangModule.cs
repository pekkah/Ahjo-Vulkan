using Ahjo.Vulkan.Slang.Internal;
using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang;

/// <summary>
/// One compiled translation unit — a <c>.slang</c> file or an in-memory source
/// string, checked and ready to be composed into a program. Wraps
/// <c>IModule</c>.
/// </summary>
/// <remarks>
/// <para><b>A module is not a program.</b> It carries no entry points into the
/// linkage by itself (<c>slang.h:5572-5575</c>), and reflecting one in
/// isolation reports different set and binding numbers than the composed
/// program does. Compose, link, and reflect the linked result.</para>
/// <para><b>Ownership.</b> <c>ISession::loadModule*</c> hands back a pointer
/// the <em>session</em> owns — measured on <c>v2026.14.1</c>: releasing the
/// returned pointer without an <c>addRef</c> first corrupts the allocator's
/// heap on session teardown. This type therefore takes its own reference on
/// construction and drops exactly that one on <see cref="Dispose"/>, which is
/// balanced whoever else holds the module.</para>
/// </remarks>
public sealed unsafe class SlangModule : IDisposable
{
    private readonly SlangSession _session;
    private IModule* _module;

    internal SlangModule(SlangSession session, IModule* module, string diagnostics)
    {
        _session = session;
        _module = module;

        // See the ownership note in the type remarks: the pointer is borrowed
        // from the session, so take a reference of our own to balance the one
        // Dispose drops.
        module->addRef();

        Name = SlangUtf8.ToString(module->getName()) ?? string.Empty;
        Warnings = string.IsNullOrEmpty(diagnostics) ? null : diagnostics;
    }

    /// <summary>The name the module is registered under in its session.</summary>
    public string Name { get; }

    /// <summary>
    /// Diagnostics Slang produced while loading this module even though the
    /// load succeeded — warnings. <see langword="null"/> when the load was
    /// silent.
    /// </summary>
    /// <remarks>
    /// A failed load throws <see cref="SlangCompilationException"/> instead,
    /// so this property never carries a fatal error.
    /// </remarks>
    public string? Warnings { get; }

    /// <summary>
    /// Number of entry points this module <em>declares</em> with a
    /// <c>[shader("…")]</c> attribute.
    /// </summary>
    public int DefinedEntryPointCount => Handle->getDefinedEntryPointCount();

    internal IModule* Handle
        => _module != null ? _module : throw new ObjectDisposedException(nameof(SlangModule));

    internal IComponentType* Component => (IComponentType*)Handle;

    internal SlangSession Session => _session;

    /// <summary>
    /// The <paramref name="index"/>-th <c>[shader("…")]</c> entry point, in
    /// declaration order.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside <see cref="DefinedEntryPointCount"/>.</exception>
    /// <exception cref="SlangCompilationException">Slang could not produce the entry point or its layout.</exception>
    public SlangEntryPoint DefinedEntryPoint(int index)
    {
        IModule* module = Handle;
        int count = module->getDefinedEntryPointCount();

        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, count);

        IEntryPoint* entryPoint = null;
        int rc = module->getDefinedEntryPoint(index, &entryPoint);

        if (rc < 0 || entryPoint == null)
        {
            throw new SlangCompilationException(
                $"Slang compilation failed: getDefinedEntryPoint({index}) on module '{Name}' returned 0x{rc:X8}.",
                string.Empty,
                innerException: null);
        }

        return SlangEntryPoint.FromReflectedStage(entryPoint);
    }

    /// <summary>
    /// Finds an entry point by name at <paramref name="stage"/>, checking it
    /// even when the function carries no <c>[shader("…")]</c> attribute.
    /// </summary>
    /// <remarks>
    /// <para><b>Slang does not validate <paramref name="stage"/> against a
    /// <c>[shader("…")]</c> attribute.</b> Measured on <c>v2026.14.1</c>:
    /// asking for a <c>[shader("fragment")]</c> function as
    /// <see cref="ShaderStages.Compute"/> returns <c>SLANG_OK</c> and the
    /// fragment entry point, with an empty diagnostics blob. The
    /// <see cref="SlangEntryPoint.Stage"/> reported back is therefore the
    /// stage <em>you asked for</em>; use
    /// <see cref="DefinedEntryPoint(int)"/> when you want the stage the
    /// shader declares.</para>
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// <paramref name="stage"/> is not a single stage Slang can look an entry
    /// point up at.
    /// </exception>
    /// <exception cref="SlangCompilationException">No such entry point.</exception>
    public SlangEntryPoint FindEntryPoint(string name, ShaderStages stage)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        // Total switch: an unmapped stage throws rather than degrading to
        // SLANG_STAGE_NONE, which Slang would accept and then miss on.
        SlangStage slangStage = SlangStages.ToSlangStage(stage);

        IModule* module = Handle;
        IEntryPoint* entryPoint = null;
        ISlangBlob* diagnostics = null;
        int rc;

        Span<byte> scratch = stackalloc byte[SlangUtf8.StackScratchBytes];
        using (var nameUtf8 = new SlangUtf8.ScopedUtf8(scratch, name))
        {
            fixed (byte* namePtr = nameUtf8.Bytes)
            {
                rc = module->findAndCheckEntryPoint((sbyte*)namePtr, slangStage, &entryPoint, &diagnostics);
            }
        }

        string text = SlangUtf8.TakeDiagnostics(&diagnostics);

        if (rc < 0 || entryPoint == null)
        {
            throw new SlangCompilationException($"findAndCheckEntryPoint('{name}', {stage}) (0x{rc:X8})", text);
        }

        return SlangEntryPoint.FromRequestedStage(entryPoint, name, stage);
    }

    /// <summary>Drops this wrapper's reference to the module.</summary>
    public void Dispose()
    {
        IModule* module = _module;

        _module = null;

        if (module != null)
        {
            module->release();
        }
    }
}
