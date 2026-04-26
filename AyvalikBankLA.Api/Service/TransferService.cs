using AyvalikBankLA.Api.Exception;
using AyvalikBankLA.Api.Model;

namespace AyvalikBankLA.Api.Service;

public class TransferService
{
    public decimal CalculateFee(decimal amount, bool sameCustomer, decimal feePercent, CustomerTier sourceTier)
    {
        if (sameCustomer) return 0m;
        var scaledPercent = feePercent * sourceTier.FeeMultiplier();
        return Math.Round(amount * scaledPercent / 100m, 2, MidpointRounding.AwayFromZero);
    }

    public void RequireTransferWithinLimit(decimal amount, CustomerTier tier)
    {
        var cap = tier.MaxPerTransfer();
        if (cap is not null && amount > cap.Value)
            throw new LimitExceededException($"Transfer amount {amount} exceeds {tier} tier limit of {cap}");
    }

    public void RequireWithdrawalWithinLimit(decimal amount, CustomerTier tier)
    {
        var cap = tier.MaxPerWithdrawal();
        if (cap is not null && amount > cap.Value)
            throw new LimitExceededException($"Withdrawal amount {amount} exceeds {tier} tier limit of {cap}");
    }
}
