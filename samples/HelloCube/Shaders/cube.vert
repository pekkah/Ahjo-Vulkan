#version 450

layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec2 inUV;
layout(location = 2) in vec3 inFaceTint;

layout(push_constant) uniform PushConstants {
    mat4 mvp;
} pc;

layout(location = 0) out vec2 vUV;
layout(location = 1) out vec3 vFaceTint;

void main() {
    gl_Position = pc.mvp * vec4(inPosition, 1.0);
    vUV         = inUV;
    vFaceTint   = inFaceTint;
}
