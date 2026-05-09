#version 450

layout(set = 0, binding = 0) uniform sampler2D uTexture;

layout(location = 0) in  vec2 vUV;
layout(location = 1) in  vec3 vFaceTint;
layout(location = 0) out vec4 outColor;

void main() {
    vec3 sampled = texture(uTexture, vUV).rgb;
    // Mild face tint kept around 0.92..1.0 so the wood pattern stays
    // readable on every face but adjacent sides don't blur into one
    // continuous surface when the cube tumbles.
    vec3 tint = mix(vec3(1.0), vFaceTint, 0.25);
    outColor  = vec4(sampled * tint, 1.0);
}
