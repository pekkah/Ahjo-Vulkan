#version 450

// The Tint UBO comes from a host-visible persistent-mapped buffer that
// HelloVma writes directly through Buffer.AsSpan<Tint>(). Demonstrates
// the typical "small per-frame uniform" memory pattern.

layout(location = 0) in  vec3 vColor;
layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 0) uniform Tint {
    vec4 tint;
} u;

void main() {
    outColor = vec4(vColor * u.tint.rgb, u.tint.a);
}
