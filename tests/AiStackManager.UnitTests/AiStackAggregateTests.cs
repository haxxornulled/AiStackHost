using AiStackManager.Domain.Configuration;
using AiStackManager.Domain.Stack;
using Xunit;

namespace AiStackManager.UnitTests;

public sealed class AiStackAggregateTests
{
    [Fact]
    public void Create_Rejects_Context_Below_Hermes_Minimum()
    {
        var result = AiStackAggregate.Create(new AiStackSettings { ContextLength = 32768 });
        Assert.True(result.IsFail);
    }

    [Fact]
    public void Create_Accepts_64k_Model_Context()
    {
        var result = AiStackAggregate.Create(new AiStackSettings { Model = "qwen25-coder-14b-64k", ContextLength = 65536 });
        Assert.True(result.IsSucc);
    }

    [Fact]
    public void BeginStart_Transitions_To_Starting()
    {
        var stack = AiStackAggregate.Create(new AiStackSettings()).Value;
        var result = stack.BeginStart();
        Assert.True(result.IsSucc);
        Assert.Equal(AiStackPhase.Starting, stack.Phase);
    }
}
