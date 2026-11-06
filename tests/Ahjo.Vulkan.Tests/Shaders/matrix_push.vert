#version 450

// Exists only so CompiledShaderTests can assert that GLSL compiled through
// Slang keeps glslc's column-major matrix layout — i.e. that the shared
// SlangShader step still passes -matrix-layout-row-major for GLSL items
// (spec E4: without it Slang's GLSL frontend silently transposes `mat4`).
// No pipeline uses it. std430 is explicit, per the GLSL push-constant rule
// in tools/CLAUDE.md.

layout(push_constant, std430) uniform Pc {
    mat4 mvp;
} pc;

layout(location = 0) in vec3 inPosition;

void main() {
    gl_Position = pc.mvp * vec4(inPosition, 1.0);
}
