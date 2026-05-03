namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineTessellationDomainOriginStateCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkTessellationDomainOrigin domainOrigin;
}
