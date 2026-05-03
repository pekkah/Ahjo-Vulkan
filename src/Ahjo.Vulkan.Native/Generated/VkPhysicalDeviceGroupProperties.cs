using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceGroupProperties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint physicalDeviceCount;

    [NativeTypeName("VkPhysicalDevice[32]")]
    public _physicalDevices_e__FixedBuffer physicalDevices;

    [NativeTypeName("VkBool32")]
    public uint subsetAllocation;

    public unsafe partial struct _physicalDevices_e__FixedBuffer
    {
        public VkPhysicalDevice_T* e0;
        public VkPhysicalDevice_T* e1;
        public VkPhysicalDevice_T* e2;
        public VkPhysicalDevice_T* e3;
        public VkPhysicalDevice_T* e4;
        public VkPhysicalDevice_T* e5;
        public VkPhysicalDevice_T* e6;
        public VkPhysicalDevice_T* e7;
        public VkPhysicalDevice_T* e8;
        public VkPhysicalDevice_T* e9;
        public VkPhysicalDevice_T* e10;
        public VkPhysicalDevice_T* e11;
        public VkPhysicalDevice_T* e12;
        public VkPhysicalDevice_T* e13;
        public VkPhysicalDevice_T* e14;
        public VkPhysicalDevice_T* e15;
        public VkPhysicalDevice_T* e16;
        public VkPhysicalDevice_T* e17;
        public VkPhysicalDevice_T* e18;
        public VkPhysicalDevice_T* e19;
        public VkPhysicalDevice_T* e20;
        public VkPhysicalDevice_T* e21;
        public VkPhysicalDevice_T* e22;
        public VkPhysicalDevice_T* e23;
        public VkPhysicalDevice_T* e24;
        public VkPhysicalDevice_T* e25;
        public VkPhysicalDevice_T* e26;
        public VkPhysicalDevice_T* e27;
        public VkPhysicalDevice_T* e28;
        public VkPhysicalDevice_T* e29;
        public VkPhysicalDevice_T* e30;
        public VkPhysicalDevice_T* e31;

        public ref VkPhysicalDevice_T* this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                fixed (VkPhysicalDevice_T** pThis = &e0)
                {
                    return ref pThis[index];
                }
            }
        }
    }
}
