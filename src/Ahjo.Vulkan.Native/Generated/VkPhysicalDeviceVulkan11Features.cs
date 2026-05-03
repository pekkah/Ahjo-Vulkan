namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceVulkan11Features
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint storageBuffer16BitAccess;

    [NativeTypeName("VkBool32")]
    public uint uniformAndStorageBuffer16BitAccess;

    [NativeTypeName("VkBool32")]
    public uint storagePushConstant16;

    [NativeTypeName("VkBool32")]
    public uint storageInputOutput16;

    [NativeTypeName("VkBool32")]
    public uint multiview;

    [NativeTypeName("VkBool32")]
    public uint multiviewGeometryShader;

    [NativeTypeName("VkBool32")]
    public uint multiviewTessellationShader;

    [NativeTypeName("VkBool32")]
    public uint variablePointersStorageBuffer;

    [NativeTypeName("VkBool32")]
    public uint variablePointers;

    [NativeTypeName("VkBool32")]
    public uint protectedMemory;

    [NativeTypeName("VkBool32")]
    public uint samplerYcbcrConversion;

    [NativeTypeName("VkBool32")]
    public uint shaderDrawParameters;
}
