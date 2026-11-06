#version 450

// Exists only so CompiledShaderTests can assert that a GLSL push-constant
// block compiled through Slang keeps glslc's std430 offsets. Slang's GLSL
// frontend lays push constants out std140 unless the block says std430, and
// no slangc flag changes that, so every GLSL push block in this repository
// carries an explicit `std430` (tools/CLAUDE.md).
//
// The block is chosen so the two layouts disagree: std430 gives arr an
// ArrayStride of 4 and c an Offset of 8; std140 gives 16 and 32.
// No pipeline uses it.

layout(push_constant, std430) uniform Pc {
    float arr[2];
    float c;
} pc;

void main() {
    gl_Position = vec4(pc.arr[0], pc.arr[1], pc.c, 1.0);
}
