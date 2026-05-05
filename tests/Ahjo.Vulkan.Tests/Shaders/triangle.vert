#version 450

// Hardcoded NDC triangle: bottom-left, bottom-right, top-center.
// No vertex inputs — gl_VertexIndex selects the corner. Used as a
// fixture for GraphicsPipelineBuilder + (later) the HelloTriangle
// integration test.

const vec2 positions[3] = vec2[](
    vec2(-0.5, -0.5),
    vec2( 0.5, -0.5),
    vec2( 0.0,  0.5)
);

void main() {
    gl_Position = vec4(positions[gl_VertexIndex], 0.0, 1.0);
}
