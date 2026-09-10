using STS2Connector.LiveHost;
using Xunit;

namespace STS2Connector.Host.Tests;

public sealed class RewardStateCompletenessTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExactEmptyRewardStateRemainsCompleteWhenProceedIsDisabled(bool hasCommand)
    {
        var state = RewardClaimSurfaceReader.DescribeCompleteness(true, hasCommand);
        Assert.Equal("contract_complete_for_reward_claim", state.PlayerVisibleSemantics);
        Assert.Empty(state.Missing);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingExactPresentationBindingNeverBecomesComplete(bool hasCommand)
    {
        var state = RewardClaimSurfaceReader.DescribeCompleteness(false, hasCommand);
        Assert.Equal("partial", state.PlayerVisibleSemantics);
        Assert.Contains("native_reward_presentation_bijection", state.Missing);
    }
}
