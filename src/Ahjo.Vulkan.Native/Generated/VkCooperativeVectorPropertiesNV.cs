namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCooperativeVectorPropertiesNV
{
    public VkStructureType sType;

    public void* pNext;

    public VkComponentTypeKHR inputType;

    public VkComponentTypeKHR inputInterpretation;

    public VkComponentTypeKHR matrixInterpretation;

    public VkComponentTypeKHR biasInterpretation;

    public VkComponentTypeKHR resultType;

    [NativeTypeName("VkBool32")]
    public uint transpose;
}
