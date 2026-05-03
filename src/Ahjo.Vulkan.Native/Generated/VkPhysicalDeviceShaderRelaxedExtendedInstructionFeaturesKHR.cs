namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderRelaxedExtendedInstructionFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderRelaxedExtendedInstruction;
}
