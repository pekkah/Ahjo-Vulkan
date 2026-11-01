#version 450

// triangle.vert's corners at clip-space z = -0.5, w = 1: in front of the near
// plane (Vulkan's clip volume is 0 <= z <= w), so the whole triangle is clipped
// unless depth clamp is on. Fixture for GraphicsPipelineBuilder.WithDepthClamp.

const vec2 positions[3] = vec2[](
    vec2(-0.5, -0.5),
    vec2( 0.5, -0.5),
    vec2( 0.0,  0.5)
);

void main() {
    gl_Position = vec4(positions[gl_VertexIndex], -0.5, 1.0);
}
