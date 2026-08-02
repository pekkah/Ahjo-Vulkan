using Ahjo.Vulkan.Slang.Internal;
using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang;

/// <summary>
/// A compilation session — one SPIR-V target, one module namespace, one set of
/// search paths. Wraps <c>ISession</c>.
/// </summary>
/// <remarks>
/// <para>Modules are cached in the session under the name they were loaded
/// with, which is what lets one in-memory module <c>import</c> another with no
/// file system present. It also means a name is claimed once: loading
/// different source under a name that is already loaded hands back the
/// <em>first</em> module, silently. Give each distinct source its own module
/// name, or use a fresh session.</para>
/// <para>Must be disposed before the <see cref="SlangCompiler"/> that created
/// it, and after everything obtained from it.</para>
/// </remarks>
public sealed unsafe class SlangSession : IDisposable
{
    private readonly SlangCompiler _compiler;
    private ISession* _session;
    private NativeUtf8Array? _searchPaths;

    internal SlangSession(SlangCompiler compiler, ISession* session, NativeUtf8Array? searchPaths)
    {
        _compiler = compiler;
        _session = session;
        _searchPaths = searchPaths;
    }

    internal ISession* Handle
        => _session != null ? _session : throw new ObjectDisposedException(nameof(SlangSession));

    /// <summary>The compiler this session belongs to.</summary>
    public SlangCompiler Compiler => _compiler;

    /// <summary>
    /// Loads a module by name, resolved through the session's
    /// <see cref="SlangSessionDescription.SearchPaths"/>.
    /// </summary>
    /// <exception cref="SlangCompilationException">The module could not be found, parsed or checked.</exception>
    public SlangModule LoadModule(string moduleName)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleName);

        ISession* session = Handle;
        IModule* module;
        ISlangBlob* diagnostics = null;
        string text;

        Span<byte> scratch = stackalloc byte[SlangUtf8.StackScratchBytes];
        using (var name = new SlangUtf8.ScopedUtf8(scratch, moduleName))
        {
            fixed (byte* namePtr = name.Bytes)
            {
                module = session->loadModule((sbyte*)namePtr, &diagnostics);
            }
        }

        text = SlangUtf8.TakeDiagnostics(&diagnostics);

        // Slang signals this failure by returning nullptr. There is no
        // SlangResult on this call at all, so a result-code-only check would
        // sail straight past a broken module and hand back an empty blob
        // later — the exact failure issue #166 exists to prevent.
        if (module == null)
        {
            throw new SlangCompilationException($"loadModule('{moduleName}')", text);
        }

        return new SlangModule(this, module, text);
    }

    /// <summary>
    /// Loads a module from in-memory UTF-8 source and registers it under
    /// <paramref name="moduleName"/>, so a later module's
    /// <c>import <paramref name="moduleName"/>;</c> resolves with no file
    /// system present.
    /// </summary>
    /// <param name="moduleName">The name the module is registered under.</param>
    /// <param name="path">
    /// The path reported in diagnostics. Need not exist; it is what the
    /// caller will see in an error message.
    /// </param>
    /// <param name="source">UTF-8 source text. Need not be null-terminated.</param>
    /// <exception cref="SlangCompilationException">The source did not parse or check.</exception>
    public SlangModule LoadModuleFromSource(string moduleName, string path, ReadOnlySpan<byte> source)
    {
        // Shader source runs to kilobytes, so it never goes on the stack:
        // an empty scratch sends ScopedUtf8 straight to the pool.
        using var text = new SlangUtf8.ScopedUtf8(default, source);

        return LoadModuleFromTerminatedSource(moduleName, path, text.Bytes);
    }

    /// <inheritdoc cref="LoadModuleFromSource(string, string, ReadOnlySpan{byte})"/>
    public SlangModule LoadModuleFromSource(string moduleName, string path, string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var text = new SlangUtf8.ScopedUtf8(default, source);

        return LoadModuleFromTerminatedSource(moduleName, path, text.Bytes);
    }

    /// <summary>
    /// Starts an explicitly composed program: N modules and N entry points, in
    /// the order the caller adds them.
    /// </summary>
    /// <remarks>
    /// <see cref="Compile"/> is the one-module convenience path. Reach for
    /// this one when the program is assembled at run time out of several
    /// modules, because the component list and its order are part of the
    /// resulting layout — see <see cref="SlangProgramBuilder"/>.
    /// </remarks>
    public SlangProgramBuilder CreateProgram()
    {
        _ = Handle;

        return new SlangProgramBuilder(this);
    }

    /// <summary>
    /// Compiles one module and links the requested entry points into a
    /// program.
    /// </summary>
    /// <remarks>
    /// The convenience path: load a module, find its entry points, composite
    /// and link. Expressed entirely in terms of
    /// <see cref="LoadModuleFromSource(string, string, string)"/>,
    /// <see cref="SlangModule"/> and <see cref="SlangProgramBuilder"/>, so there
    /// is exactly one code path that loads a module, one that finds an entry
    /// point, and one that composes components.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Neither or both of <see cref="SlangCompileRequest.Path"/> and
    /// <see cref="SlangCompileRequest.Source"/> were set, or a name in
    /// <see cref="SlangCompileRequest.TypeConformances"/> does not name a type
    /// in the composed program.
    /// </exception>
    /// <exception cref="SlangCompilationException">Slang refused at any step.</exception>
    public SlangProgram Compile(in SlangCompileRequest request)
    {
        bool hasPath = !string.IsNullOrEmpty(request.Path);
        bool hasSource = request.Source is not null;

        if (hasPath == hasSource)
        {
            throw new ArgumentException(
                "Set exactly one of SlangCompileRequest.Path and SlangCompileRequest.Source.",
                nameof(request));
        }

        string path = hasPath ? request.Path! : (request.ModuleName ?? "module") + ".slang";
        string moduleName = request.ModuleName
            ?? (hasPath ? System.IO.Path.GetFileNameWithoutExtension(path) : "module");
        string source = hasSource ? request.Source! : File.ReadAllText(request.Path!);

        using SlangModule module = LoadModuleFromSource(moduleName, path, source);

        SlangEntryPoint[] entryPoints = SelectEntryPoints(module, request.EntryPoints);

        try
        {
            // One composition path, and it is SlangProgramBuilder's: the
            // component list is [module, ep₀, ep₁, …] in both, and the order is
            // the layout. A second implementation of it here could drift.
            SlangProgramBuilder builder = CreateProgram().Add(module);

            for (int i = 0; i < entryPoints.Length; i++)
            {
                builder.Add(entryPoints[i]);
            }

            if (request.TypeConformances is { Count: > 0 } conformances)
            {
                for (int i = 0; i < conformances.Count; i++)
                {
                    builder.AddTypeConformance(conformances[i].ConcreteType, conformances[i].InterfaceType);
                }
            }

            return builder.Link(module.Warnings);
        }
        finally
        {
            for (int i = 0; i < entryPoints.Length; i++)
            {
                entryPoints[i]?.Dispose();
            }
        }
    }

    /// <summary>Releases the session.</summary>
    public void Dispose()
    {
        ISession* session = _session;

        _session = null;

        if (session != null)
        {
            session->release();
        }

        _searchPaths?.Dispose();
        _searchPaths = null;
    }

    /// <param name="terminatedSource">UTF-8 source whose last byte is <c>0</c>.</param>
    private SlangModule LoadModuleFromTerminatedSource(
        string moduleName,
        string path,
        ReadOnlySpan<byte> terminatedSource)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleName);
        ArgumentException.ThrowIfNullOrEmpty(path);

        ISession* session = Handle;
        IModule* module;
        ISlangBlob* diagnostics = null;

        Span<byte> nameScratch = stackalloc byte[SlangUtf8.StackScratchBytes];
        Span<byte> pathScratch = stackalloc byte[SlangUtf8.StackScratchBytes];

        using (var name = new SlangUtf8.ScopedUtf8(nameScratch, moduleName))
        using (var pathUtf8 = new SlangUtf8.ScopedUtf8(pathScratch, path))
        {
            fixed (byte* namePtr = name.Bytes)
            fixed (byte* pathPtr = pathUtf8.Bytes)
            fixed (byte* textPtr = terminatedSource)
            {
                module = session->loadModuleFromSourceString(
                    (sbyte*)namePtr, (sbyte*)pathPtr, (sbyte*)textPtr, &diagnostics);
            }
        }

        string diagnosticsText = SlangUtf8.TakeDiagnostics(&diagnostics);

        if (module == null)
        {
            throw new SlangCompilationException($"loadModuleFromSourceString('{moduleName}')", diagnosticsText);
        }

        return new SlangModule(this, module, diagnosticsText);
    }

    private static SlangEntryPoint[] SelectEntryPoints(SlangModule module, IReadOnlyList<string>? requested)
    {
        int definedCount = module.DefinedEntryPointCount;

        if (requested is null)
        {
            if (definedCount == 0)
            {
                throw new SlangCompilationException(
                    $"Slang compilation failed: module '{module.Name}' defines no [shader(\"…\")] entry points.",
                    string.Empty,
                    innerException: null);
            }

            var all = new SlangEntryPoint[definedCount];

            try
            {
                for (int i = 0; i < definedCount; i++)
                {
                    all[i] = module.DefinedEntryPoint(i);
                }
            }
            catch
            {
                for (int i = 0; i < all.Length; i++)
                {
                    all[i]?.Dispose();
                }

                throw;
            }

            return all;
        }

        // A requested name carries no stage, and findAndCheckEntryPoint needs
        // one — so the module's own [shader("…")] declarations are what supply
        // it. Looking the name up here rather than letting Slang miss it also
        // lets the failure name the entry points that DO exist.
        var defined = new SlangEntryPoint[definedCount];
        var selected = new SlangEntryPoint[requested.Count];

        try
        {
            for (int i = 0; i < definedCount; i++)
            {
                defined[i] = module.DefinedEntryPoint(i);
            }

            for (int i = 0; i < requested.Count; i++)
            {
                string name = requested[i];
                ShaderStages stage = default;
                bool found = false;

                for (int d = 0; d < definedCount; d++)
                {
                    if (string.Equals(defined[d].Name, name, StringComparison.Ordinal))
                    {
                        stage = defined[d].Stage;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    throw new SlangCompilationException(
                        $"Slang compilation failed: module '{module.Name}' has no [shader(\"…\")] entry point named '{name}'. Defined: {DescribeDefined(defined, definedCount)}.",
                        string.Empty,
                        innerException: null);
                }

                selected[i] = module.FindEntryPoint(name, stage);
            }
        }
        catch
        {
            for (int i = 0; i < selected.Length; i++)
            {
                selected[i]?.Dispose();
            }

            throw;
        }
        finally
        {
            for (int i = 0; i < defined.Length; i++)
            {
                defined[i]?.Dispose();
            }
        }

        return selected;
    }

    private static string DescribeDefined(SlangEntryPoint[] defined, int count)
    {
        if (count == 0)
        {
            return "(none)";
        }

        var names = new string[count];

        for (int i = 0; i < count; i++)
        {
            names[i] = defined[i].Name;
        }

        return string.Join(", ", names);
    }
}
