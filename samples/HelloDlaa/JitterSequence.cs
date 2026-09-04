using System.Numerics;

namespace Ahjo.Vulkan.Samples.HelloDlaa;

/// <summary>
/// The sub-pixel jitter DLSS reconstructs from: a Halton(2,3) sequence in
/// render pixels, and the one function that applies it to a view-projection
/// matrix.
/// </summary>
/// <remarks>
/// <para>The table is built once, in the constructor. Nothing here allocates
/// per frame — <see cref="Current"/> is an index into an array that exists
/// before the loop starts.</para>
/// <para>Phase count is the programming guide's §3.7.1.1 formula,
/// <c>8 * (target / render)^2</c>: 8 at DLAA, 18 at Quality's 1.5x linear
/// ratio.</para>
/// </remarks>
internal sealed class JitterSequence
{
    private readonly Vector2[] _offsets;
    private int _index;

    public JitterSequence(uint renderWidth, uint renderHeight, uint outputWidth, uint outputHeight)
    {
        double ratio = renderWidth == 0 ? 1.0 : outputWidth / (double)renderWidth;
        PhaseCount = Math.Max(8, (int)Math.Ceiling(8.0 * ratio * ratio));

        _offsets = new Vector2[PhaseCount];
        for (int i = 0; i < PhaseCount; i++)
        {
            // Halton is 1-based; the -0.5 recentres [0,1) on [-0.5,+0.5), which
            // is the range guide §3.7.3 requires the reported offsets to be in.
            _offsets[i] = new Vector2(Halton(i + 1, 2) - 0.5f, Halton(i + 1, 3) - 0.5f);
        }
    }

    /// <summary>Guide §3.7.1.1 — 8 at DLAA, 18 at Quality.</summary>
    public int PhaseCount { get; }

    /// <summary>
    /// This frame's offset, in render pixels, each component in
    /// <c>[-0.5, +0.5]</c>. Goes into
    /// <c>DlssEvaluateInputs.JitterOffsetX/Y</c> unchanged — see
    /// <see cref="ApplyJitter"/> for why "unchanged" is the correct answer.
    /// </summary>
    public Vector2 Current => _offsets[_index];

    public void Advance()
    {
        _index++;
        if (_index >= PhaseCount) _index = 0;
    }

    /// <summary>
    /// Returns <paramref name="viewProjection"/> with a sub-pixel shift of
    /// <paramref name="jitterPixels"/> applied, by post-multiplying a
    /// clip-space translation.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this form and not the guide's
    /// <c>ProjectionMatrix.M[2][0] += ProjectionJitter.X</c> (§3.7.2).</b>
    /// <see cref="Matrix4x4.CreatePerspectiveFieldOfView"/> is right-handed
    /// with <c>M34 = -1</c>, so <c>clip.w = -z</c> and
    /// <c>ndc.x = (x·M11 + z·M31) / (-z) = -x·M11/z - M31</c>: adding <c>δ</c>
    /// to <c>M31</c> shifts NDC by <b>minus</b> <c>δ</c>. Both the sign and the
    /// magnitude of the guide's recipe are therefore convention-dependent.</para>
    /// <para>Post-multiplying by a clip-space translation is immune to both.
    /// <c>System.Numerics</c> composes row-vectors (<c>v * (A*B) == (v*A)*B</c>,
    /// which is why the matrices read <c>model * view * proj</c>), so
    /// <c>clip' = clip * t</c> gives <c>clip'.x = clip.x + clip.w * t.M41</c>
    /// and hence <c>ndc'.x = ndc.x + t.M41</c> exactly — no dependence on the
    /// sign of <c>w</c>. With <c>t.M41 = 2·jx / renderWidth</c> the image moves
    /// by exactly <c>+jx</c> pixels, which is what makes passing
    /// <see cref="Current"/> to NGX unchanged correct: §3.7.3 defines
    /// <c>JitterOffset</c> as "the jitter applied to the projection matrix", in
    /// render pixels, in the motion-vector coordinate system.</para>
    /// <para>Do not "simplify" this back to the guide's form. Spec D2 step 2
    /// records the algebra for exactly that reason.</para>
    /// </remarks>
    public static Matrix4x4 ApplyJitter(
        in Matrix4x4 viewProjection,
        Vector2      jitterPixels,
        uint         renderWidth,
        uint         renderHeight)
    {
        Matrix4x4 t = Matrix4x4.Identity;
        t.M41 = 2f * jitterPixels.X / renderWidth;
        t.M42 = 2f * jitterPixels.Y / renderHeight;
        return viewProjection * t;
    }

    /// <summary>
    /// The radical-inverse Halton sequence. <paramref name="index"/> is 1-based
    /// — Halton(0, r) is 0 for every radix, which would waste a phase on the
    /// unjittered sample.
    /// </summary>
    internal static float Halton(int index, int radix)
    {
        float result   = 0f;
        float fraction = 1f / radix;
        int   i        = index;

        while (i > 0)
        {
            result   += (i % radix) * fraction;
            i        /= radix;
            fraction /= radix;
        }

        return result;
    }
}
