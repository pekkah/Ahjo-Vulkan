namespace Ahjo.Vulkan.Slang.Tests;

/// <summary>
/// Ground truth for the reflection suite: the <c>OpDecorate</c> instructions in
/// the module Slang actually emitted.
/// </summary>
/// <remarks>
/// <para>Reflection agreeing with itself proves nothing. Every claim this
/// package makes about a descriptor set number, a binding number or a vertex
/// input location is a claim about what the driver will see, and the only
/// artifact that carries that is the SPIR-V. So the load-bearing reflection
/// tests assert against decorations read out of the blob rather than against
/// numbers reflection reported.</para>
/// <para>This is a decoration reader, not a SPIR-V parser: it walks the
/// instruction stream by word count and picks out three opcodes. Anything else
/// it steps over.</para>
/// </remarks>
internal static class SpirvDecorations
{
    private const uint Magic = 0x07230203;
    private const int HeaderWords = 5;

    private const uint OpName = 5;
    private const uint OpMemberName = 6;
    private const uint OpEntryPoint = 15;
    private const uint OpVariable = 59;
    private const uint OpDecorate = 71;
    private const uint OpMemberDecorate = 72;

    private const uint DecorationLocation = 30;
    private const uint DecorationBinding = 33;
    private const uint DecorationDescriptorSet = 34;
    private const uint DecorationOffset = 35;

    private const uint StorageClassInput = 1;

    /// <summary>
    /// Every <c>(set, binding)</c> pair the module decorates, with the
    /// <c>OpName</c> of the variable it belongs to.
    /// </summary>
    /// <remarks>
    /// Per-entry-point SPIR-V contains only what that entry point uses, so this
    /// is a subset of what the program declares — which is the right direction
    /// for the assertion "everything the shader binds is something reflection
    /// reported".
    /// </remarks>
    public static List<(uint Set, uint Binding, string Name)> ReadDescriptorBindings(ReadOnlySpan<uint> words)
    {
        Dictionary<uint, string> names = ReadNames(words);
        var sets = new Dictionary<uint, uint>();
        var bindings = new Dictionary<uint, uint>();

        foreach (Instruction instruction in Instructions(words))
        {
            if (instruction.Opcode != OpDecorate || instruction.Operands.Length < 3)
            {
                continue;
            }

            switch (instruction.Operands[1])
            {
                case DecorationDescriptorSet:
                    sets[instruction.Operands[0]] = instruction.Operands[2];

                    break;

                case DecorationBinding:
                    bindings[instruction.Operands[0]] = instruction.Operands[2];

                    break;

                default:
                    break;
            }
        }

        var result = new List<(uint, uint, string)>();

        foreach ((uint id, uint set) in sets)
        {
            if (bindings.TryGetValue(id, out uint binding))
            {
                result.Add((set, binding, names.TryGetValue(id, out string? name) ? name : $"<id {id}>"));
            }
        }

        result.Sort(static (a, b) => a.Item1 != b.Item1
            ? a.Item1.CompareTo(b.Item1)
            : a.Item2.CompareTo(b.Item2));

        return result;
    }

    /// <summary>
    /// Every <c>Location</c> decoration on an <b>input</b> variable, ascending
    /// by location — what a vertex stage's
    /// <c>VkVertexInputAttributeDescription.Location</c> values have to match.
    /// </summary>
    /// <remarks>
    /// Filtering on the <c>Input</c> storage class is not optional: a vertex
    /// stage's outputs carry <c>Location</c> decorations from the same
    /// numbering space, so a reader that ignored storage class would report an
    /// output at location 0 and an input at location 0 as the same thing.
    /// Built-ins (<c>SV_Position</c> and friends) carry <c>BuiltIn</c> rather
    /// than <c>Location</c> and drop out for free.
    /// </remarks>
    public static List<(uint Location, string Name)> ReadInputLocations(ReadOnlySpan<uint> words)
    {
        Dictionary<uint, string> names = ReadNames(words);
        var locations = new Dictionary<uint, uint>();
        var inputs = new HashSet<uint>();

        foreach (Instruction instruction in Instructions(words))
        {
            if (instruction.Opcode == OpDecorate
                && instruction.Operands.Length >= 3
                && instruction.Operands[1] == DecorationLocation)
            {
                locations[instruction.Operands[0]] = instruction.Operands[2];
            }
            else if (instruction.Opcode == OpVariable
                && instruction.Operands.Length >= 3
                && instruction.Operands[2] == StorageClassInput)
            {
                inputs.Add(instruction.Operands[1]);
            }
        }

        var result = new List<(uint, string)>();

        foreach ((uint id, uint location) in locations)
        {
            if (inputs.Contains(id))
            {
                result.Add((location, names.TryGetValue(id, out string? name) ? name : $"<id {id}>"));
            }
        }

        result.Sort(static (a, b) => a.Item1.CompareTo(b.Item1));

        return result;
    }

    /// <summary>
    /// Every <c>OpMemberDecorate … Offset</c> in the module, with the struct's
    /// <c>OpName</c> and the member's <c>OpMemberName</c> when the module
    /// carries them.
    /// </summary>
    /// <remarks>
    /// <para><b>This is the oracle issue #175 exists for.</b> The suite could
    /// read member offsets back out of reflection and compare them to
    /// themselves; that is what let a <c>float2</c> → <c>float4</c> widening
    /// pass a 97-test suite untouched. A byte offset is a claim about what the
    /// driver will read, and the only artifact carrying that is the SPIR-V.</para>
    /// <para>Offsets here are <b>struct-relative</b>, the way SPIR-V states
    /// them: a member of a nested struct is decorated relative to that struct,
    /// not to the buffer. <c>SlangBufferMember.Offset</c> is buffer-absolute,
    /// so a comparison subtracts the parent's offset.</para>
    /// <para>Measured on <c>v2026.14.1</c> / win-x64: Slang <b>does</b> emit
    /// <c>OpMemberName</c> for these fixtures, so members are matched by name.
    /// The pre-committed fallback — comparing the ordered offset sequence of
    /// the struct whose member count matches — was not needed; if a future
    /// Slang stops emitting them, <c>MemberName</c> comes back empty and that
    /// is the signal to switch.</para>
    /// </remarks>
    public static List<(string StructName, string MemberName, uint Index, uint Offset)> ReadMemberOffsets(
        ReadOnlySpan<uint> words)
    {
        Dictionary<uint, string> names = ReadNames(words);
        var memberNames = new Dictionary<(uint Type, uint Index), string>();

        foreach (Instruction instruction in Instructions(words))
        {
            if (instruction.Opcode == OpMemberName && instruction.Operands.Length >= 3)
            {
                memberNames[(instruction.Operands[0], instruction.Operands[1])] =
                    ReadLiteralString(instruction.Operands[2..]);
            }
        }

        var result = new List<(string, string, uint, uint)>();

        foreach (Instruction instruction in Instructions(words))
        {
            if (instruction.Opcode != OpMemberDecorate
                || instruction.Operands.Length < 4
                || instruction.Operands[2] != DecorationOffset)
            {
                continue;
            }

            uint type = instruction.Operands[0];
            uint index = instruction.Operands[1];

            result.Add((
                names.TryGetValue(type, out string? structName) ? structName : $"<id {type}>",
                memberNames.TryGetValue((type, index), out string? memberName) ? memberName : string.Empty,
                index,
                instruction.Operands[3]));
        }

        return result;
    }

    /// <summary>
    /// The name of every <c>OpEntryPoint</c> in the module — what
    /// <c>VkPipelineShaderStageCreateInfo.pName</c> has to match.
    /// </summary>
    public static List<string> ReadEntryPointNames(ReadOnlySpan<uint> words)
    {
        var result = new List<string>();

        foreach (Instruction instruction in Instructions(words))
        {
            if (instruction.Opcode == OpEntryPoint && instruction.Operands.Length >= 3)
            {
                result.Add(ReadLiteralString(instruction.Operands[2..]));
            }
        }

        return result;
    }

    private static Dictionary<uint, string> ReadNames(ReadOnlySpan<uint> words)
    {
        var names = new Dictionary<uint, string>();

        foreach (Instruction instruction in Instructions(words))
        {
            if (instruction.Opcode == OpName && instruction.Operands.Length >= 2)
            {
                names[instruction.Operands[0]] = ReadLiteralString(instruction.Operands[1..]);
            }
        }

        return names;
    }

    /// <summary>
    /// A SPIR-V literal string: UTF-8 packed four bytes per word, little-endian
    /// within the word, null-terminated and zero-padded to a word boundary.
    /// </summary>
    private static string ReadLiteralString(ReadOnlySpan<uint> words)
    {
        Span<byte> bytes = stackalloc byte[words.Length * 4];

        for (int i = 0; i < words.Length; i++)
        {
            uint word = words[i];

            bytes[(i * 4) + 0] = (byte)word;
            bytes[(i * 4) + 1] = (byte)(word >> 8);
            bytes[(i * 4) + 2] = (byte)(word >> 16);
            bytes[(i * 4) + 3] = (byte)(word >> 24);
        }

        int end = bytes.IndexOf((byte)0);

        return System.Text.Encoding.UTF8.GetString(end < 0 ? bytes : bytes[..end]);
    }

    private static InstructionEnumerator Instructions(ReadOnlySpan<uint> words) => new(words);

    /// <summary>One instruction: its opcode and its operand words.</summary>
    private readonly ref struct Instruction(uint opcode, ReadOnlySpan<uint> operands)
    {
        public uint Opcode { get; } = opcode;

        public ReadOnlySpan<uint> Operands { get; } = operands;
    }

    /// <summary>
    /// Walks the instruction stream. A <c>ref struct</c> enumerator so the
    /// <c>foreach</c> can hand out <see cref="ReadOnlySpan{T}"/> operands
    /// without copying.
    /// </summary>
    private ref struct InstructionEnumerator
    {
        private readonly ReadOnlySpan<uint> _words;
        private int _offset;

        public InstructionEnumerator(ReadOnlySpan<uint> words)
        {
            if (words.Length < HeaderWords || words[0] != Magic)
            {
                throw new ArgumentException(
                    $"Not a SPIR-V module: {words.Length} words, first word 0x{(words.Length > 0 ? words[0] : 0):X8}.",
                    nameof(words));
            }

            _words = words;
            _offset = HeaderWords;
            Current = default;
        }

        public Instruction Current { get; private set; }

        public readonly InstructionEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            if (_offset >= _words.Length)
            {
                return false;
            }

            uint header = _words[_offset];
            int wordCount = (int)(header >> 16);

            if (wordCount < 1 || _offset + wordCount > _words.Length)
            {
                return false;
            }

            Current = new Instruction(header & 0xFFFF, _words.Slice(_offset + 1, wordCount - 1));
            _offset += wordCount;

            return true;
        }
    }
}
