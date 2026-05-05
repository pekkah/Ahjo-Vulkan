using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class QueueRequestTests
{
    [Fact]
    public void Ctor_ValidInputs_RoundTrips()
    {
        var r = new QueueRequest(familyIndex: 2, count: 3, priority: 0.5f);
        Assert.Equal(2u,   r.FamilyIndex);
        Assert.Equal(3u,   r.Count);
        Assert.Equal(0.5f, r.Priority);
    }

    [Fact]
    public void Ctor_ZeroCount_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new QueueRequest(0, 0, 0.5f));
        Assert.Equal("count", ex.ParamName);
    }

    [Fact]
    public void Ctor_NegativePriority_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new QueueRequest(0, 1, -0.0001f));
        Assert.Equal("priority", ex.ParamName);
    }

    [Fact]
    public void Ctor_PriorityOver1_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new QueueRequest(0, 1, 1.0001f));
        Assert.Equal("priority", ex.ParamName);
    }

    [Fact]
    public void Ctor_NaNPriority_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new QueueRequest(0, 1, float.NaN));
        Assert.Equal("priority", ex.ParamName);
    }
}
