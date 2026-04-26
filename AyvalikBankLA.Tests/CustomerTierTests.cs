using AyvalikBankLA.Api.Model;
using FluentAssertions;
using Xunit;

namespace AyvalikBankLA.Tests;

public class CustomerTierTests
{
    [Fact]
    public void StandardHasFullFeeAndModestCaps()
    {
        CustomerTier.STANDARD.FeeMultiplier().Should().Be(1.00m);
        CustomerTier.STANDARD.MaxPerTransfer().Should().Be(5000m);
        CustomerTier.STANDARD.MaxPerWithdrawal().Should().Be(5000m);
    }

    [Fact]
    public void PremiumHalvesFeeAndRaisesCaps()
    {
        CustomerTier.PREMIUM.FeeMultiplier().Should().Be(0.50m);
        CustomerTier.PREMIUM.MaxPerTransfer().Should().Be(50000m);
        CustomerTier.PREMIUM.MaxPerWithdrawal().Should().Be(25000m);
    }

    [Fact]
    public void PrivateIsFreeAndUnlimited()
    {
        CustomerTier.PRIVATE.FeeMultiplier().Should().Be(0.00m);
        CustomerTier.PRIVATE.MaxPerTransfer().Should().BeNull();
        CustomerTier.PRIVATE.MaxPerWithdrawal().Should().BeNull();
    }
}
