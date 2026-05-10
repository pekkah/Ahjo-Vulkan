#version 450

layout(location = 0) in  vec3 vColor;
layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 0) uniform Frame {
    mat4 transform;
    vec4 tint;
} u;

void main() {
    outColor = vec4(vColor * u.tint.rgb, u.tint.a);
}
