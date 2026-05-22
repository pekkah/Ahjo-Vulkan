using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Ahjo.Vulkan.StructExtendsGen;

// Reads vk.xml + the existing ClangSharp-generated VkStructureType.cs and
// emits one partial-struct file per `<type ... structextends="...">` entry.
//
// Output:
//   - For each chainable struct X with structextends = [A, B, C]:
//       partial struct X : IChainable<A>, IChainable<B>, IChainable<C>
//       {
//           public X() { sType = VK_STRUCTURE_TYPE_X; }  // only when X isn't also a root
//           public static VkStructureType SType => VK_STRUCTURE_TYPE_X;
//       }
//   - For each unique target T appearing in any structextends:
//       partial struct T : IChainRoot
//       {
//           public T() { sType = VK_STRUCTURE_TYPE_T; }
//           public static VkStructureType RootSType => VK_STRUCTURE_TYPE_T;
//       }
//
// The parameterless ctor makes `new VkX { ... }` object-initializer syntax
// write a valid sType (issue #94). Four structs are both roots and
// chainables; the ctor lives in their Root.g.cs partial only, so the two
// partial declarations don't both declare a parameterless ctor (CS0111).
//
// `default(T)`, `stackalloc`, and array-element initialization still
// produce sType=0 — they don't run user-defined struct ctors. Caller
// responsibility there; out of scope for this fix.
//
// Skipped:
//   - Aliases (alias attribute set).
//   - Structs whose sType identifier is not present in VkStructureType.cs
//     (i.e. gated behind a platform we don't ship — VK_USE_PLATFORM_*).
//   - Structs whose ClangSharp emit is absent — these are platform-gated
//     types (Android/Win32/Metal/FUCHSIA/QNX/OHOS/GGP) whose sType enum
//     value is unconditional but whose struct body is not. Writing a
//     parameterless ctor that touches `sType` would fail to compile.
//   - structextends targets whose sType is similarly missing.
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("Usage: Ahjo.Vulkan.StructExtendsGen <vk.xml> <VkStructureType.cs> <output-dir>");
            return 2;
        }

        var vkXmlPath = args[0];
        var structureTypePath = args[1];
        var outputDir = args[2];

        var availableSTypes = ReadAvailableSTypes(structureTypePath);
        Console.WriteLine($"VkStructureType.cs declares {availableSTypes.Count} sType values.");

        var clangSharpOutputDir = Path.GetDirectoryName(structureTypePath)
            ?? throw new InvalidOperationException("structureTypePath has no directory.");
        var availableStructs = ReadAvailableStructs(clangSharpOutputDir);
        Console.WriteLine($"ClangSharp emit declares {availableStructs.Count} structs in {clangSharpOutputDir}.");

        var (chainables, structToSType) = ReadChainables(vkXmlPath);
        Console.WriteLine($"vk.xml lists {chainables.Count} structs with non-empty structextends.");

        Directory.CreateDirectory(outputDir);

        // Clear existing chain partials so removed entries (after a vk.xml
        // bump) don't linger. We own this directory exclusively.
        foreach (var stale in Directory.GetFiles(outputDir, "*.cs"))
        {
            File.Delete(stale);
        }

        var emittedRoots = new HashSet<string>(StringComparer.Ordinal);
        var skippedForMissingSType = 0;
        var skippedForMissingStruct = 0;
        var skippedForMissingTargetSType = 0;
        var skippedForMissingTargetStruct = 0;

        // Pre-compute the root set so EmitExtender knows whether to skip the
        // parameterless ctor (it lives in Root.g.cs for dual-membership structs).
        var includedEntries = new List<(ChainEntry entry, List<string> targets)>();
        foreach (var entry in chainables.OrderBy(e => e.StructName, StringComparer.Ordinal))
        {
            if (!availableSTypes.Contains(entry.STypeIdentifier))
            {
                skippedForMissingSType++;
                continue;
            }
            if (!availableStructs.Contains(entry.StructName))
            {
                skippedForMissingStruct++;
                continue;
            }

            var includedTargets = new List<string>(entry.Targets.Length);
            foreach (var target in entry.Targets)
            {
                if (!structToSType.TryGetValue(target, out var targetSType)
                    || !availableSTypes.Contains(targetSType))
                {
                    skippedForMissingTargetSType++;
                    continue;
                }
                if (!availableStructs.Contains(target))
                {
                    skippedForMissingTargetStruct++;
                    continue;
                }
                includedTargets.Add(target);
            }

            if (includedTargets.Count == 0)
            {
                continue;
            }

            includedEntries.Add((entry, includedTargets));
            foreach (var target in includedTargets)
            {
                emittedRoots.Add(target);
            }
        }

        var emittedExtenders = 0;
        foreach (var (entry, includedTargets) in includedEntries)
        {
            var alsoRoot = emittedRoots.Contains(entry.StructName);
            EmitExtender(outputDir, entry.StructName, entry.STypeIdentifier, includedTargets, alsoRoot);
            emittedExtenders++;
        }

        foreach (var rootName in emittedRoots.OrderBy(s => s, StringComparer.Ordinal))
        {
            EmitRoot(outputDir, rootName, structToSType[rootName]);
        }

        Console.WriteLine($"Emitted {emittedExtenders} IChainable partials and {emittedRoots.Count} IChainRoot partials.");
        Console.WriteLine($"Skipped: {skippedForMissingSType} structs (sType absent from VkStructureType.cs), {skippedForMissingStruct} structs (struct body absent from ClangSharp emit), {skippedForMissingTargetSType} target slots (target sType absent), {skippedForMissingTargetStruct} target slots (target struct body absent).");
        return 0;
    }

    private static HashSet<string> ReadAvailableStructs(string clangSharpOutputDir)
    {
        // ClangSharp emits one file per struct, named "<StructName>.cs". The
        // filename is a reliable proxy for the struct's emitted body — much
        // cheaper than parsing every file. We restrict to top-level entries
        // (not the Chains/ subdirectory we own).
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(clangSharpOutputDir, "Vk*.cs", SearchOption.TopDirectoryOnly))
        {
            set.Add(Path.GetFileNameWithoutExtension(path));
        }
        return set;
    }

    private static HashSet<string> ReadAvailableSTypes(string path)
    {
        // VkStructureType.cs lines look like:
        //     VK_STRUCTURE_TYPE_APPLICATION_INFO = 0,
        // We just want the leading identifier per non-empty enum-body line.
        var pattern = new Regex(@"^\s*(VK_STRUCTURE_TYPE_[A-Z0-9_]+)\s*=", RegexOptions.Compiled);
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(path))
        {
            var match = pattern.Match(line);
            if (match.Success)
            {
                set.Add(match.Groups[1].Value);
            }
        }
        return set;
    }

    private record struct ChainEntry(string StructName, string STypeIdentifier, string[] Targets);

    private static (List<ChainEntry> entries, Dictionary<string, string> structToSType) ReadChainables(string path)
    {
        var doc = XDocument.Load(path);
        var entries = new List<ChainEntry>();
        var structToSType = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var type in doc.Descendants("type"))
        {
            if ((string?)type.Attribute("category") != "struct") continue;
            if (type.Attribute("alias") is not null) continue;

            var name = (string?)type.Attribute("name");
            if (string.IsNullOrEmpty(name)) continue;

            // Find sType member.
            string? sTypeIdentifier = null;
            foreach (var member in type.Elements("member"))
            {
                var typeText = member.Element("type")?.Value;
                var nameText = member.Element("name")?.Value;
                if (typeText == "VkStructureType" && nameText == "sType")
                {
                    sTypeIdentifier = (string?)member.Attribute("values");
                    break;
                }
            }
            if (string.IsNullOrEmpty(sTypeIdentifier)) continue;

            structToSType[name] = sTypeIdentifier;

            var structextends = (string?)type.Attribute("structextends");
            if (string.IsNullOrEmpty(structextends)) continue;

            var targets = structextends.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            entries.Add(new ChainEntry(name, sTypeIdentifier, targets));
        }

        return (entries, structToSType);
    }

    private static void EmitExtender(string outputDir, string structName, string sType, List<string> targets, bool alsoRoot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// Source: vk.xml structextends → IChainable<TRoot>.");
        sb.AppendLine("// Edits to this file are overwritten by RegenerateChains.");
        sb.AppendLine("namespace Ahjo.Vulkan.Native;");
        sb.AppendLine();
        sb.Append("public unsafe partial struct ").Append(structName);
        sb.Append(" : ");
        for (var i = 0; i < targets.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append("IChainable<").Append(targets[i]).Append('>');
        }
        sb.AppendLine();
        sb.AppendLine("{");
        if (!alsoRoot)
        {
            EmitParameterlessCtor(sb, structName, sType);
        }
        sb.Append("    public static VkStructureType SType => VkStructureType.").Append(sType).AppendLine(";");
        sb.AppendLine("}");

        File.WriteAllText(Path.Combine(outputDir, structName + ".Chain.g.cs"), sb.ToString());
    }

    private static void EmitRoot(string outputDir, string structName, string sType)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// Source: vk.xml structextends targets → IChainRoot.");
        sb.AppendLine("// Edits to this file are overwritten by RegenerateChains.");
        sb.AppendLine("namespace Ahjo.Vulkan.Native;");
        sb.AppendLine();
        sb.Append("public unsafe partial struct ").Append(structName).AppendLine(" : IChainRoot");
        sb.AppendLine("{");
        EmitParameterlessCtor(sb, structName, sType);
        sb.Append("    public static VkStructureType RootSType => VkStructureType.").Append(sType).AppendLine(";");
        sb.AppendLine("}");

        File.WriteAllText(Path.Combine(outputDir, structName + ".Root.g.cs"), sb.ToString());
    }

    private static void EmitParameterlessCtor(StringBuilder sb, string structName, string sType)
    {
        sb.Append("    public ").Append(structName).AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        sType = VkStructureType.").Append(sType).AppendLine(";");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
}
