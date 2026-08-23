// Issue 207: the only executable form of VK_KHR_ray_query.
//
// The extension defines zero entry points, so the wrapper has no ray-query API
// to test — traversal happens entirely here. Everything Ahjo.Vulkan contributes
// (the BLAS and TLAS builds, the instance's device address, the AS descriptor
// write, the barrier that makes the build visible to this shader) is upstream
// of the first line of this file, and this file is what proves any of it was
// right.
//
// Slang rather than GLSL: ray query in GLSL needs GL_EXT_ray_query and a glslc
// invocation, and the repository is moving off glslc. Ahjo.Vulkan.Slang
// compiles this at run time with `spvRayQueryKHR` declared as a target
// capability, so the compile is warning-free.

// The TLAS built on the host. `RaytracingAccelerationStructure` lowers to
// VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR, which is what
// DescriptorWrite.AccelerationStructure writes.
[[vk::binding(0, 0)]] RaytracingAccelerationStructure scene;

// The image the traversal result is written to, copied back and saved as a PNG.
[[vk::binding(1, 0)]] RWTexture2D<float4> output;

static const float3 kMissColor = float3(0.05, 0.07, 0.10);

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 tid : SV_DispatchThreadID)
{
    uint width, height;
    output.GetDimensions(width, height);
    if (tid.x >= width || tid.y >= height)
        return;

    // Pixel centre -> [-1, 1]. Y is flipped so +Y is up in the saved PNG,
    // which keeps the image the same way round as the vertex data reads.
    float2 uv  = (float2(tid.xy) + 0.5) / float2(width, height);
    float2 ndc = float2(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0);

    // Orthographic: every ray points straight down +Z at the z = 0 plane the
    // triangles live in. A hit therefore means "this pixel is inside a
    // triangle", which is what makes the output image a picture of the
    // geometry that was actually built rather than a picture of a shading
    // choice.
    RayDesc ray;
    ray.Origin    = float3(ndc, -1.0);
    ray.Direction = float3(0.0, 0.0, 1.0);
    ray.TMin      = 0.0;
    ray.TMax      = 10.0;

    RayQuery<RAY_FLAG_FORCE_OPAQUE> q;
    q.TraceRayInline(scene, RAY_FLAG_NONE, 0xFF, ray);

    // Every geometry here is opaque triangles, so traversal never yields a
    // candidate the shader must resolve — one Proceed() drains it.
    q.Proceed();

    float3 color = kMissColor;
    if (q.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
    {
        // Barycentrics tint the hit so a wrong vertex layout shows up as wrong
        // colour, not just as a differently-shaped blob; the primitive index
        // separates the two triangles.
        float2 bary = q.CommittedTriangleBarycentrics();
        float3 rgb  = float3(1.0 - bary.x - bary.y, bary.x, bary.y);
        color = q.CommittedPrimitiveIndex() == 0 ? rgb : rgb.zxy;
    }

    output[tid.xy] = float4(color, 1.0);
}
