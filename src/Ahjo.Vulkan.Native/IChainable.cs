namespace Ahjo.Vulkan.Native;

/// <summary>
/// Compile-time contract: <typeparamref name="TRoot"/> can host this struct
/// in its <c>pNext</c> chain. Every implementer is generated from the
/// <c>vk.xml</c> registry's <c>structextends</c> attribute (one
/// <see cref="IChainable{TRoot}"/> instantiation per legal target), so
/// the C# compiler enforces structural validity without a runtime check.
/// </summary>
/// <remarks>
/// <para><see cref="SType"/> is a single static abstract — a struct that
/// extends multiple roots implements <see cref="IChainable{TRoot}"/> for
/// each, but there is one <c>SType</c> on the type that satisfies all of
/// them.</para>
/// <para>Whether a chain is *also* legal at runtime depends on whether the
/// owning extension/version is enabled on the Vulkan device. The type
/// system catches structural misuse (chaining onto an incompatible head);
/// the validation layers catch the rest.</para>
/// </remarks>
public interface IChainable<TRoot>
    where TRoot : unmanaged
{
    static abstract VkStructureType SType { get; }
}

/// <summary>
/// Compile-time marker for a struct that can serve as the head of a
/// <c>pNext</c> chain (i.e. appears as a <c>structextends</c> destination
/// in <c>vk.xml</c>). Carries the head's <see cref="VkStructureType"/>
/// so wrappers don't need it as a runtime argument.
/// </summary>
public interface IChainRoot
{
    static abstract VkStructureType RootSType { get; }
}
