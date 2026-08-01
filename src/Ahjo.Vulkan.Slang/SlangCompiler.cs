using Ahjo.Vulkan.Slang.Internal;
using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang;

/// <summary>
/// The Slang compiler itself — a wrapper over <c>IGlobalSession</c>, which is
/// what loads the core module and owns everything downstream.
/// </summary>
/// <remarks>
/// <para>Create one and keep it: constructing a global session loads and
/// checks Slang's embedded core module, which is the expensive part of the
/// whole pipeline. A <see cref="SlangSession"/> — the thing that actually
/// compiles — is cheap by comparison.</para>
/// <para><b>Lifetime.</b> Everything obtained from this compiler
/// (<see cref="SlangSession"/>, <see cref="SlangModule"/>,
/// <see cref="SlangProgram"/> …) must be disposed <em>before</em> the compiler
/// is. <see cref="Dispose"/> releases the global session and deliberately does
/// <b>not</b> call <c>slang_shutdown()</c>: shutdown is process-scoped and
/// would make a second <see cref="Create"/> in the same process undefined.
/// Creating, disposing and creating again is supported and covered by a test
/// (issue #166, OPEN-3).</para>
/// <para><b>Allocation posture.</b> Everything in this package is setup-time.
/// The wrapper's zero-per-frame-allocation invariant covers
/// <c>Recording/</c>, <c>Sync/</c>, <c>Pools/</c> and <c>Memory/</c>; nothing
/// here is reachable from a frame loop and no benchmark covers it.</para>
/// </remarks>
public sealed unsafe class SlangCompiler : IDisposable
{
    private IGlobalSession* _global;

    private SlangCompiler(IGlobalSession* global) => _global = global;

    /// <summary>
    /// The version string the loaded native binary reports, e.g.
    /// <c>"2026.14.1"</c>. This is the pinned <c>SlangVersion</c> without its
    /// leading <c>v</c>.
    /// </summary>
    public string BuildTag
    {
        get
        {
            IGlobalSession* global = Handle;

            return SlangUtf8.ToString(global->getBuildTagString()) ?? string.Empty;
        }
    }

    internal IGlobalSession* Handle
        => _global != null ? _global : throw new ObjectDisposedException(nameof(SlangCompiler));

    /// <summary>Loads the Slang compiler and creates its global session.</summary>
    /// <exception cref="SlangCompilationException">
    /// The global session could not be created. The native library itself
    /// failing to load surfaces earlier, as a <see cref="DllNotFoundException"/>
    /// naming <c>slang</c>.
    /// </exception>
    public static SlangCompiler Create()
    {
        IGlobalSession* global = null;
        int rc = SlangApi.slang_createGlobalSession(0, &global);

        if (rc < 0 || global == null)
        {
            throw new SlangCompilationException(
                $"Slang compilation failed: slang_createGlobalSession returned 0x{rc:X8}.",
                string.Empty,
                innerException: null);
        }

        return new SlangCompiler(global);
    }

    /// <summary>
    /// Creates a compilation session targeting SPIR-V as described by
    /// <paramref name="description"/>.
    /// </summary>
    /// <remarks>
    /// The returned session must be disposed before this compiler is.
    /// </remarks>
    /// <exception cref="SlangCompilationException">
    /// The profile name is not one Slang knows, or session creation failed.
    /// </exception>
    public SlangSession CreateSession(in SlangSessionDescription description)
    {
        IGlobalSession* global = Handle;

        // Compile-time constant, so invariant #1 in its literal form.
        Utf8Name profileName = description.SpirvProfile.IsNull
            ? Utf8Name.FromLiteral("spirv_1_5"u8)
            : description.SpirvProfile;

        SlangProfileID profile = global->findProfile(profileName.Ptr);

        if (profile == SlangProfileID.SLANG_PROFILE_UNKNOWN)
        {
            throw new SlangCompilationException(
                $"Slang compilation failed: unknown SPIR-V profile '{SlangUtf8.ToString(profileName.Ptr)}'.",
                string.Empty,
                innerException: null);
        }

        NativeUtf8Array? searchPaths = NativeUtf8Array.Create(description.SearchPaths);

        try
        {
            // Optimization rides as a target compiler option rather than a
            // TargetDesc field — TargetDesc has no optimization member, and
            // CompilerOptionName.Optimization is the documented route. The
            // public enum's values are the native SlangOptimizationLevel's, so
            // the cast is the mapping.
            CompilerOptionEntry optimization = default;
            optimization.name = CompilerOptionName.Optimization;
            optimization.value.kind = CompilerOptionValueKind.Int;
            optimization.value.intValue0 = (int)description.Optimization;

            // structureSize is NOT optional on either struct. Both carry C++
            // default member initialisers upstream, ClangSharp does not
            // reproduce those, and Slang reads the field to decide how much of
            // the struct it may look at.
            TargetDesc target = default;
            target.structureSize = (nuint)sizeof(TargetDesc);
            target.format = SlangCompileTarget.SLANG_SPIRV;
            target.profile = profile;
            target.flags = description.EmitSpirvDirectly ? SlangApi.SLANG_TARGET_FLAG_GENERATE_SPIRV_DIRECTLY : 0u;
            target.compilerOptionEntries = &optimization;
            target.compilerOptionEntryCount = 1;

            SessionDesc desc = default;
            desc.structureSize = (nuint)sizeof(SessionDesc);
            desc.targets = &target;
            desc.targetCount = 1;
            desc.searchPaths = searchPaths?.Pointers;
            desc.searchPathCount = searchPaths?.Count ?? 0;

            ISession* session = null;
            int rc = global->createSession(&desc, &session);

            if (rc < 0 || session == null)
            {
                throw new SlangCompilationException(
                    $"Slang compilation failed: createSession returned 0x{rc:X8}.",
                    string.Empty,
                    innerException: null);
            }

            var created = new SlangSession(this, session, searchPaths);

            searchPaths = null;

            return created;
        }
        finally
        {
            searchPaths?.Dispose();
        }
    }

    /// <summary>
    /// Releases the global session. Does not call <c>slang_shutdown()</c> —
    /// see the type remarks.
    /// </summary>
    public void Dispose()
    {
        IGlobalSession* global = _global;

        _global = null;

        if (global != null)
        {
            global->release();
        }
    }
}
