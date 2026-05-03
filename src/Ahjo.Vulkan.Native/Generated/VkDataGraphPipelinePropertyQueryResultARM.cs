namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphPipelinePropertyQueryResultARM
{
    public VkStructureType sType;

    public void* pNext;

    public VkDataGraphPipelinePropertyARM property;

    [NativeTypeName("VkBool32")]
    public uint isText;

    [NativeTypeName("size_t")]
    public nuint dataSize;

    public void* pData;
}
