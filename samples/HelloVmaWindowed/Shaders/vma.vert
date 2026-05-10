#version 450

// Reads positions + colors from the static device-local vertex buffer
// uploaded once at startup. The per-frame UBO supplies a 2D rotation +
// uniform scale + aspect correction baked into a mat4, plus a tint
// vector consumed by the fragment shader.

layout(location = 0) in vec2 inPos;
layout(location = 1) in vec3 inColor;

layout(location = 0) out vec3 vColor;

layout(set = 0, binding = 0) uniform Frame {
    mat4 transform;
    vec4 tint;
} u;

void main() {
    gl_Position = u.transform * vec4(inPos, 0.0, 1.0);
    vColor      = inColor;
}
