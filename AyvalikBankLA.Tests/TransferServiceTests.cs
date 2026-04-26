using AyvalikBankLA.Api.Exception;
using AyvalikBankLA.Api.Model;
using AyvalikBankLA.Api.Service;
using FluentAssertions;
using Xunit;

namespace AyvalikBankLA.Tests;

public class TransferServiceTests
{
    private readonly TransferService _service = new();

    [Fact]
    public void SameCustomerTransferIsFreeRegardlessOfTier()
    {
        _service.CalculateFee(200m, true, 1.0m, CustomerTier.STANDARD).Should().Be(0m);
    }

    [Fact]
    public void StandardTierPaysFullFee()
    {
        _service.CalculateFee(200m, false, 1.0m, CustomerTier.STANDARD).Should().Be(2.00m);
    }

    [Fact]
    public void PremiumTierPaysHalfFee()
    {
        _service.CalculateFee(200m, false, 1.0m, CustomerTier.PREMIUM).Should().Be(1.00m);
    }

    [Fact]
    public void PrivateTierPaysNoFee()
    {
        _service.CalculateFee(200m, false, 1.0m, CustomerTier.PRIVATE).Should().Be(0m);
    }

    [Fact]
    public void RejectsTransferAboveStandardCap()
    {
        var act = () => _service.RequireTransferWithinLimit(5001m, CustomerTier.STANDARD);
        act.Should().Throw<LimitExceededException>().WithMessage("*STANDARD*");
    }

    [Fact]
    public void AllowsTransferAtExactlyTheCap()
    {
        var act = () => _service.RequireTransferWithinLimit(5000m, CustomerTier.STANDARD);
        act.Should().NotThrow();
    }

    [Fact]
    public void PrivateTierTransferIsUnlimited()
    {
        var act = () => _service.RequireTransferWithinLimit(1_000_000m, CustomerTier.PRIVATE);
        act.Should().NotThrow();
    }

    [Fact]
    public void RejectsWithdrawalAbovePremiumCap()
    {
        var act = () => _service.RequireWithdrawalWithinLimit(25001m, CustomerTier.PREMIUM);
        act.Should().Throw<LimitExceededException>().WithMessage("*PREMIUM*");
    }
}
