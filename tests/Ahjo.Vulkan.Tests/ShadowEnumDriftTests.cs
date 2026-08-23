using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Vma.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// The wrapper's public flag/usage enums (<see cref="BufferUsage"/>,
/// <see cref="ImageUsage"/>, <see cref="AllocationFlags"/>,
/// <see cref="MemoryUsage"/>, <see cref="ShaderStages"/>,
/// <see cref="DescriptorBindingFlags"/>) hand-copy their numeric values from
/// the native Vulkan / VMA enums rather than aliasing them, so the public API
/// reads idiomatically and stays decoupled from the generated bindings'
/// naming. Correct today — but a Vulkan-Headers / VMA bump plus a regen could
/// silently renumber a native member and desynchronize a shadow value.
///
/// These explicit asserts pin each shadow member to its native counterpart so
/// any drift becomes a compile error (a renamed/removed native member) or a
/// test failure (a renumbered value) in CI, instead of a runtime bug that
/// passes the wrong bit to the driver. No reflection — every pair is spelled
/// out, so the suite stays trim/AOT-irrelevant and the failure message points
/// straight at the offending member (issue #122).
/// </summary>
public sealed class ShadowEnumDriftTests
{
    [Fact]
    public void BufferUsage_MatchesNative()
    {
        Assert.Equal((uint)VkBufferUsageFlagBits.VK_BUFFER_USAGE_TRANSFER_SRC_BIT, (uint)BufferUsage.TransferSrc);
        Assert.Equal((uint)VkBufferUsageFlagBits.VK_BUFFER_USAGE_TRANSFER_DST_BIT, (uint)BufferUsage.TransferDst);
        Assert.Equal((uint)VkBufferUsageFlagBits.VK_BUFFER_USAGE_UNIFORM_TEXEL_BUFFER_BIT, (uint)BufferUsage.UniformTexelBuffer);
        Assert.Equal((uint)VkBufferUsageFlagBits.VK_BUFFER_USAGE_STORAGE_TEXEL_BUFFER_BIT, (uint)BufferUsage.StorageTexelBuffer);
        Assert.Equal((uint)VkBufferUsageFlagBits.VK_BUFFER_USAGE_UNIFORM_BUFFER_BIT, (uint)BufferUsage.UniformBuffer);
        Assert.Equal((uint)VkBufferUsageFlagBits.VK_BUFFER_USAGE_STORAGE_BUFFER_BIT, (uint)BufferUsage.StorageBuffer);
        Assert.Equal((uint)VkBufferUsageFlagBits.VK_BUFFER_USAGE_INDEX_BUFFER_BIT, (uint)BufferUsage.IndexBuffer);
        Assert.Equal((uint)VkBufferUsageFlagBits.VK_BUFFER_USAGE_VERTEX_BUFFER_BIT, (uint)BufferUsage.VertexBuffer);
        Assert.Equal((uint)VkBufferUsageFlagBits.VK_BUFFER_USAGE_INDIRECT_BUFFER_BIT, (uint)BufferUsage.IndirectBuffer);
        Assert.Equal((uint)VkBufferUsageFlagBits.VK_BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT, (uint)BufferUsage.ShaderDeviceAddress);
        Assert.Equal((uint)VkBufferUsageFlagBits.VK_BUFFER_USAGE_ACCELERATION_STRUCTURE_BUILD_INPUT_READ_ONLY_BIT_KHR, (uint)BufferUsage.AccelerationStructureBuildInputReadOnly);
        Assert.Equal((uint)VkBufferUsageFlagBits.VK_BUFFER_USAGE_ACCELERATION_STRUCTURE_STORAGE_BIT_KHR, (uint)BufferUsage.AccelerationStructureStorage);
        Assert.Equal((uint)VkBufferUsageFlagBits.VK_BUFFER_USAGE_SHADER_BINDING_TABLE_BIT_KHR, (uint)BufferUsage.ShaderBindingTable);
    }

    [Fact]
    public void ImageUsage_MatchesNative()
    {
        Assert.Equal((uint)VkImageUsageFlagBits.VK_IMAGE_USAGE_TRANSFER_SRC_BIT, (uint)ImageUsage.TransferSrc);
        Assert.Equal((uint)VkImageUsageFlagBits.VK_IMAGE_USAGE_TRANSFER_DST_BIT, (uint)ImageUsage.TransferDst);
        Assert.Equal((uint)VkImageUsageFlagBits.VK_IMAGE_USAGE_SAMPLED_BIT, (uint)ImageUsage.Sampled);
        Assert.Equal((uint)VkImageUsageFlagBits.VK_IMAGE_USAGE_STORAGE_BIT, (uint)ImageUsage.Storage);
        Assert.Equal((uint)VkImageUsageFlagBits.VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT, (uint)ImageUsage.ColorAttachment);
        Assert.Equal((uint)VkImageUsageFlagBits.VK_IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT, (uint)ImageUsage.DepthStencilAttachment);
        Assert.Equal((uint)VkImageUsageFlagBits.VK_IMAGE_USAGE_TRANSIENT_ATTACHMENT_BIT, (uint)ImageUsage.TransientAttachment);
        Assert.Equal((uint)VkImageUsageFlagBits.VK_IMAGE_USAGE_INPUT_ATTACHMENT_BIT, (uint)ImageUsage.InputAttachment);
        Assert.Equal((uint)VkImageUsageFlagBits.VK_IMAGE_USAGE_HOST_TRANSFER_BIT, (uint)ImageUsage.HostTransfer);
        Assert.Equal((uint)VkImageUsageFlagBits.VK_IMAGE_USAGE_FRAGMENT_SHADING_RATE_ATTACHMENT_BIT_KHR, (uint)ImageUsage.FragmentShadingRateAttachment);
    }

    [Fact]
    public void AllocationFlags_MatchesNative()
    {
        Assert.Equal((uint)VmaAllocationCreateFlagBits.VMA_ALLOCATION_CREATE_DEDICATED_MEMORY_BIT, (uint)AllocationFlags.DedicatedMemory);
        Assert.Equal((uint)VmaAllocationCreateFlagBits.VMA_ALLOCATION_CREATE_NEVER_ALLOCATE_BIT, (uint)AllocationFlags.NeverAllocate);
        Assert.Equal((uint)VmaAllocationCreateFlagBits.VMA_ALLOCATION_CREATE_MAPPED_BIT, (uint)AllocationFlags.Mapped);
        Assert.Equal((uint)VmaAllocationCreateFlagBits.VMA_ALLOCATION_CREATE_USER_DATA_COPY_STRING_BIT, (uint)AllocationFlags.UserDataCopyString);
        Assert.Equal((uint)VmaAllocationCreateFlagBits.VMA_ALLOCATION_CREATE_UPPER_ADDRESS_BIT, (uint)AllocationFlags.UpperAddress);
        Assert.Equal((uint)VmaAllocationCreateFlagBits.VMA_ALLOCATION_CREATE_DONT_BIND_BIT, (uint)AllocationFlags.DontBind);
        Assert.Equal((uint)VmaAllocationCreateFlagBits.VMA_ALLOCATION_CREATE_WITHIN_BUDGET_BIT, (uint)AllocationFlags.WithinBudget);
        Assert.Equal((uint)VmaAllocationCreateFlagBits.VMA_ALLOCATION_CREATE_CAN_ALIAS_BIT, (uint)AllocationFlags.CanAlias);
        Assert.Equal((uint)VmaAllocationCreateFlagBits.VMA_ALLOCATION_CREATE_HOST_ACCESS_SEQUENTIAL_WRITE_BIT, (uint)AllocationFlags.HostAccessSequentialWrite);
        Assert.Equal((uint)VmaAllocationCreateFlagBits.VMA_ALLOCATION_CREATE_HOST_ACCESS_RANDOM_BIT, (uint)AllocationFlags.HostAccessRandom);
        Assert.Equal((uint)VmaAllocationCreateFlagBits.VMA_ALLOCATION_CREATE_HOST_ACCESS_ALLOW_TRANSFER_INSTEAD_BIT, (uint)AllocationFlags.HostAccessAllowTransferInstead);
        Assert.Equal((uint)VmaAllocationCreateFlagBits.VMA_ALLOCATION_CREATE_STRATEGY_MIN_MEMORY_BIT, (uint)AllocationFlags.StrategyMinMemory);
        Assert.Equal((uint)VmaAllocationCreateFlagBits.VMA_ALLOCATION_CREATE_STRATEGY_MIN_TIME_BIT, (uint)AllocationFlags.StrategyMinTime);
        Assert.Equal((uint)VmaAllocationCreateFlagBits.VMA_ALLOCATION_CREATE_STRATEGY_MIN_OFFSET_BIT, (uint)AllocationFlags.StrategyMinOffset);
    }

    [Fact]
    public void MemoryUsage_MatchesNative()
    {
        Assert.Equal((uint)VmaMemoryUsage.VMA_MEMORY_USAGE_UNKNOWN, (uint)MemoryUsage.Unknown);
        Assert.Equal((uint)VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_LAZILY_ALLOCATED, (uint)MemoryUsage.GpuLazilyAllocated);
        Assert.Equal((uint)VmaMemoryUsage.VMA_MEMORY_USAGE_AUTO, (uint)MemoryUsage.Auto);
        Assert.Equal((uint)VmaMemoryUsage.VMA_MEMORY_USAGE_AUTO_PREFER_DEVICE, (uint)MemoryUsage.AutoPreferDevice);
        Assert.Equal((uint)VmaMemoryUsage.VMA_MEMORY_USAGE_AUTO_PREFER_HOST, (uint)MemoryUsage.AutoPreferHost);
    }

    [Fact]
    public void ShaderStages_MatchesNative()
    {
        Assert.Equal((uint)VkShaderStageFlagBits.VK_SHADER_STAGE_VERTEX_BIT, (uint)ShaderStages.Vertex);
        Assert.Equal((uint)VkShaderStageFlagBits.VK_SHADER_STAGE_TESSELLATION_CONTROL_BIT, (uint)ShaderStages.TessellationControl);
        Assert.Equal((uint)VkShaderStageFlagBits.VK_SHADER_STAGE_TESSELLATION_EVALUATION_BIT, (uint)ShaderStages.TessellationEval);
        Assert.Equal((uint)VkShaderStageFlagBits.VK_SHADER_STAGE_GEOMETRY_BIT, (uint)ShaderStages.Geometry);
        Assert.Equal((uint)VkShaderStageFlagBits.VK_SHADER_STAGE_FRAGMENT_BIT, (uint)ShaderStages.Fragment);
        Assert.Equal((uint)VkShaderStageFlagBits.VK_SHADER_STAGE_COMPUTE_BIT, (uint)ShaderStages.Compute);
        Assert.Equal((uint)VkShaderStageFlagBits.VK_SHADER_STAGE_TASK_BIT_EXT, (uint)ShaderStages.Task);
        Assert.Equal((uint)VkShaderStageFlagBits.VK_SHADER_STAGE_MESH_BIT_EXT, (uint)ShaderStages.Mesh);
        Assert.Equal((uint)VkShaderStageFlagBits.VK_SHADER_STAGE_ALL_GRAPHICS, (uint)ShaderStages.AllGraphics);
        Assert.Equal((uint)VkShaderStageFlagBits.VK_SHADER_STAGE_ALL, (uint)ShaderStages.All);
    }

    [Fact]
    public void DescriptorBindingFlags_MatchesNative()
    {
        Assert.Equal((uint)VkDescriptorBindingFlagBits.VK_DESCRIPTOR_BINDING_UPDATE_AFTER_BIND_BIT, (uint)DescriptorBindingFlags.UpdateAfterBind);
        Assert.Equal((uint)VkDescriptorBindingFlagBits.VK_DESCRIPTOR_BINDING_UPDATE_UNUSED_WHILE_PENDING_BIT, (uint)DescriptorBindingFlags.UpdateUnusedWhilePending);
        Assert.Equal((uint)VkDescriptorBindingFlagBits.VK_DESCRIPTOR_BINDING_PARTIALLY_BOUND_BIT, (uint)DescriptorBindingFlags.PartiallyBound);
        Assert.Equal((uint)VkDescriptorBindingFlagBits.VK_DESCRIPTOR_BINDING_VARIABLE_DESCRIPTOR_COUNT_BIT, (uint)DescriptorBindingFlags.VariableDescriptorCount);
    }

    [Fact]
    public void EventCreateFlags_MatchesNative()
    {
        Assert.Equal((uint)VkEventCreateFlagBits.VK_EVENT_CREATE_DEVICE_ONLY_BIT, (uint)EventCreateFlags.DeviceOnly);
        Assert.Equal(0u, (uint)EventCreateFlags.None);
    }

    [Fact]
    public void MemoryProperties_MatchesNative()
    {
        Assert.Equal((uint)VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT, (uint)MemoryProperties.DeviceLocal);
        Assert.Equal((uint)VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT, (uint)MemoryProperties.HostVisible);
        Assert.Equal((uint)VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_COHERENT_BIT, (uint)MemoryProperties.HostCoherent);
        Assert.Equal((uint)VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_CACHED_BIT, (uint)MemoryProperties.HostCached);
        Assert.Equal((uint)VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_LAZILY_ALLOCATED_BIT, (uint)MemoryProperties.LazilyAllocated);
        Assert.Equal((uint)VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_PROTECTED_BIT, (uint)MemoryProperties.Protected);
        Assert.Equal(0u, (uint)MemoryProperties.None);
    }

    [Fact]
    public void QueryType_MatchesNative()
    {
        Assert.Equal((int)VkQueryType.VK_QUERY_TYPE_TIMESTAMP, (int)QueryType.Timestamp);
        Assert.Equal(
            (int)VkQueryType.VK_QUERY_TYPE_ACCELERATION_STRUCTURE_COMPACTED_SIZE_KHR,
            (int)QueryType.AccelerationStructureCompactedSize);

        // Unknown is the borrowed-pool sentinel. 0 is safe only because
        // VkQueryType 0 is VK_QUERY_TYPE_OCCLUSION and this wrapper never
        // creates an occlusion pool — if that ever changes, this assert is the
        // place the conflict surfaces.
        Assert.Equal(0, (int)QueryType.Unknown);
        Assert.Equal(0, (int)VkQueryType.VK_QUERY_TYPE_OCCLUSION);
    }

    [Fact]
    public void Stage_AccelerationStructureBits_MatchNative()
    {
        Assert.Equal(
            Vk.VK_PIPELINE_STAGE_2_ACCELERATION_STRUCTURE_BUILD_BIT_KHR,
            (ulong)Stage.AccelerationStructureBuild);
        Assert.Equal(
            Vk.VK_PIPELINE_STAGE_2_ACCELERATION_STRUCTURE_COPY_BIT_KHR,
            (ulong)Stage.AccelerationStructureCopy);
    }

    [Fact]
    public void Access_AccelerationStructureBits_MatchNative()
    {
        Assert.Equal(
            Vk.VK_ACCESS_2_ACCELERATION_STRUCTURE_READ_BIT_KHR,
            (ulong)Access.AccelerationStructureRead);
        Assert.Equal(
            Vk.VK_ACCESS_2_ACCELERATION_STRUCTURE_WRITE_BIT_KHR,
            (ulong)Access.AccelerationStructureWrite);
    }

    [Fact]
    public void AccelerationStructureType_MatchesNative()
    {
        Assert.Equal(
            (int)VkAccelerationStructureTypeKHR.VK_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL_KHR,
            (int)AccelerationStructureType.TopLevel);
        Assert.Equal(
            (int)VkAccelerationStructureTypeKHR.VK_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL_KHR,
            (int)AccelerationStructureType.BottomLevel);
        Assert.Equal(
            (int)VkAccelerationStructureTypeKHR.VK_ACCELERATION_STRUCTURE_TYPE_GENERIC_KHR,
            (int)AccelerationStructureType.Generic);

        // The footgun the recorder's type/kind guard exists to catch: Vulkan
        // numbers TOP_LEVEL 0, so default(AccelerationStructureType) is a TLAS.
        Assert.Equal(AccelerationStructureType.TopLevel, default(AccelerationStructureType));
    }

    [Fact]
    public void AccelerationStructureBuildFlags_MatchesNative()
    {
        Assert.Equal(
            (uint)VkBuildAccelerationStructureFlagBitsKHR.VK_BUILD_ACCELERATION_STRUCTURE_ALLOW_UPDATE_BIT_KHR,
            (uint)AccelerationStructureBuildFlags.AllowUpdate);
        Assert.Equal(
            (uint)VkBuildAccelerationStructureFlagBitsKHR.VK_BUILD_ACCELERATION_STRUCTURE_ALLOW_COMPACTION_BIT_KHR,
            (uint)AccelerationStructureBuildFlags.AllowCompaction);
        Assert.Equal(
            (uint)VkBuildAccelerationStructureFlagBitsKHR.VK_BUILD_ACCELERATION_STRUCTURE_PREFER_FAST_TRACE_BIT_KHR,
            (uint)AccelerationStructureBuildFlags.PreferFastTrace);
        Assert.Equal(
            (uint)VkBuildAccelerationStructureFlagBitsKHR.VK_BUILD_ACCELERATION_STRUCTURE_PREFER_FAST_BUILD_BIT_KHR,
            (uint)AccelerationStructureBuildFlags.PreferFastBuild);
        Assert.Equal(
            (uint)VkBuildAccelerationStructureFlagBitsKHR.VK_BUILD_ACCELERATION_STRUCTURE_LOW_MEMORY_BIT_KHR,
            (uint)AccelerationStructureBuildFlags.LowMemory);
        Assert.Equal(0u, (uint)AccelerationStructureBuildFlags.None);
    }

    [Fact]
    public void AccelerationStructureBuildMode_MatchesNative()
    {
        Assert.Equal(
            (int)VkBuildAccelerationStructureModeKHR.VK_BUILD_ACCELERATION_STRUCTURE_MODE_BUILD_KHR,
            (int)AccelerationStructureBuildMode.Build);
        Assert.Equal(
            (int)VkBuildAccelerationStructureModeKHR.VK_BUILD_ACCELERATION_STRUCTURE_MODE_UPDATE_KHR,
            (int)AccelerationStructureBuildMode.Update);
    }

    [Fact]
    public void GeometryFlags_MatchesNative()
    {
        Assert.Equal(
            (uint)VkGeometryFlagBitsKHR.VK_GEOMETRY_OPAQUE_BIT_KHR,
            (uint)GeometryFlags.Opaque);
        Assert.Equal(
            (uint)VkGeometryFlagBitsKHR.VK_GEOMETRY_NO_DUPLICATE_ANY_HIT_INVOCATION_BIT_KHR,
            (uint)GeometryFlags.NoDuplicateAnyHitInvocation);
        Assert.Equal(0u, (uint)GeometryFlags.None);
    }

    [Fact]
    public void GeometryKind_MatchesNative()
    {
        Assert.Equal((int)VkGeometryTypeKHR.VK_GEOMETRY_TYPE_TRIANGLES_KHR, (int)GeometryKind.Triangles);
        Assert.Equal((int)VkGeometryTypeKHR.VK_GEOMETRY_TYPE_AABBS_KHR, (int)GeometryKind.Aabbs);
        Assert.Equal((int)VkGeometryTypeKHR.VK_GEOMETRY_TYPE_INSTANCES_KHR, (int)GeometryKind.Instances);
    }

    [Fact]
    public void AccelerationStructureCopyMode_MatchesNative()
    {
        Assert.Equal(
            (int)VkCopyAccelerationStructureModeKHR.VK_COPY_ACCELERATION_STRUCTURE_MODE_CLONE_KHR,
            (int)AccelerationStructureCopyMode.Clone);
        Assert.Equal(
            (int)VkCopyAccelerationStructureModeKHR.VK_COPY_ACCELERATION_STRUCTURE_MODE_COMPACT_KHR,
            (int)AccelerationStructureCopyMode.Compact);
    }
}
