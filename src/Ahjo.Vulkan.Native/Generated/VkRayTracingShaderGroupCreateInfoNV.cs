namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRayTracingShaderGroupCreateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkRayTracingShaderGroupTypeKHR type;

    [NativeTypeName("uint32_t")]
    public uint generalShader;

    [NativeTypeName("uint32_t")]
    public uint closestHitShader;

    [NativeTypeName("uint32_t")]
    public uint anyHitShader;

    [NativeTypeName("uint32_t")]
    public uint intersectionShader;
}
