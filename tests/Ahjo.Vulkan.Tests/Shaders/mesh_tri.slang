// Mesh-shading fixtures: meshMain -> mesh_tri.mesh.spv, taskMain ->
// mesh_tri.task.spv (two SlangShader items, one per entry point).
//
// Slang, not GLSL, because Slang's GLSL frontend rejects GL_EXT_mesh_shader
// (the layout(triangles, max_vertices, max_primitives) qualifier and
// EmitMeshTasksEXT), and the shared build step compiles everything with the
// pinned slangc (#171; spec E5).
//
// meshMain emits the same hard-coded NDC triangle as triangle.vert —
// bottom-left, bottom-right, top-center — so the mesh and classic paths are
// visually comparable. One workgroup, one primitive, three vertices; no
// inputs at all, which is the point: a mesh shader has no vertex-input stage.
//
// uScale is a real specialization constant so
// GraphicsPipelineBuilder.WithMeshSpecialization has something to bind to;
// its default of 1.0 leaves the emitted triangle identical to triangle.vert.
//
// taskMain is a minimal amplification stage: one workgroup in, one mesh
// workgroup out. It exists so the builder's task+mesh path has a real SPIR-V
// module to feed vkCreateGraphicsPipelines; it adds no amplification of its
// own.

[vk::constant_id(0)] const float uScale = 1.0;

static const float2 kPositions[3] = {
    float2(-0.5, -0.5),
    float2( 0.5, -0.5),
    float2( 0.0,  0.5),
};

struct MeshVertex
{
    float4 position : SV_Position;
};

[shader("mesh")]
[outputtopology("triangle")]
[numthreads(1, 1, 1)]
void meshMain(out vertices MeshVertex verts[3], out indices uint3 tris[1])
{
    SetMeshOutputCounts(3, 1);
    verts[0].position = float4(kPositions[0] * uScale, 0.0, 1.0);
    verts[1].position = float4(kPositions[1] * uScale, 0.0, 1.0);
    verts[2].position = float4(kPositions[2] * uScale, 0.0, 1.0);
    tris[0] = uint3(0, 1, 2);
}

struct TaskPayload
{
    uint unused;
};

[shader("amplification")]
[numthreads(1, 1, 1)]
void taskMain()
{
    TaskPayload payload;
    payload.unused = 0;
    DispatchMesh(1, 1, 1, payload);
}
