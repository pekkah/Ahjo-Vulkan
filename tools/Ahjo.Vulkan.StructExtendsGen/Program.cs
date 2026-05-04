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
//       { public static VkStructureType SType => VK_STRUCTURE_TYPE_X; }
//   - For each unique target T appearing in any structextends:
//       partial struct T : IChainRoot
//       { public static VkStructureType RootSType => VK_STRUCTURE_TYPE_T; }
//
// Skipped:
//   - Aliases (alias attribute set).
//   - Structs whose sType identifier is not present in VkStructureType.cs
//     (i.e. gated behind a platform we don't ship — VK_USE_PLATFORM_*).
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

        var (chainables, structToSType) = ReadChainables(vkXmlPath);
        Console.WriteLine($"vk.xml lists {chainables.Count} structs with non-empty structextends.");

        Directory.CreateDirectory(outputDir);

        // Clear existing chain partials so removed entries (after a vk.xml
        // bump) don't linger. We own this directory exclusively.
        foreach (var stale in Directory.GetFiles(outputDir, "*.cs"))
        {
            File.Delete(stale);
        }

        var emittedExtenders = 0;
        var emittedRoots = new HashSet<string>(StringComparer.Ordinal);
        var skippedForMissingSType = 0;
        var skippedForMissingTargetSType = 0;

        foreach (var entry in chainables.OrderBy(e => e.StructName, StringComparer.Ordinal))
        {
            if (!availableSTypes.Contains(entry.STypeIdentifier))
            {
                skippedForMissingSType++;
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
                includedTargets.Add(target);
            }

            if (includedTargets.Count == 0)
            {
                continue;
            }

            EmitExtender(outputDir, entry.StructName, entry.STypeIdentifier, includedTargets);
            emittedExtenders++;

            foreach (var target in includedTargets)
            {
                emittedRoots.Add(target);
            }
        }

        foreach (var rootName in emittedRoots.OrderBy(s => s, StringComparer.Ordinal))
        {
            EmitRoot(outputDir, rootName, structToSType[rootName]);
        }

        Console.WriteLine($"Emitted {emittedExtenders} IChainable partials and {emittedRoots.Count} IChainRoot partials.");
        Console.WriteLine($"Skipped: {skippedForMissingSType} structs (sType absent from VkStructureType.cs), {skippedForMissingTargetSType} target slots (target sType absent).");
        return 0;
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

    private static void EmitExtender(string outputDir, string structName, string sType, List<string> targets)
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
        sb.Append("    public static VkStructureType RootSType => VkStructureType.").Append(sType).AppendLine(";");
        sb.AppendLine("}");

        File.WriteAllText(Path.Combine(outputDir, structName + ".Root.g.cs"), sb.ToString());
    }
}
