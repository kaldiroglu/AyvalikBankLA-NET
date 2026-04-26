using AyvalikBankLA.Api.Service;
using FluentAssertions;
using Xunit;

namespace AyvalikBankLA.Tests;

public class TransferServiceTests
{
    private readonly TransferService _service = new();

    [Fact]
    public void SameCustomerTransferIsFree()
    {
        _service.CalculateFee(200m, sameCustomer: true, feePercent: 1.0m).Should().Be(0m);
    }

    [Fact]
    public void CrossCustomerTransferAppliesPercent()
    {
        _service.CalculateFee(200m, sameCustomer: false, feePercent: 1.0m).Should().Be(2.00m);
    }

    [Fact]
    public void ZeroPercentReturnsZero()
    {
        _service.CalculateFee(200m, sameCustomer: false, feePercent: 0m).Should().Be(0m);
    }
}
