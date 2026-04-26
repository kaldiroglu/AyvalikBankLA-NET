namespace AyvalikBankLA.Api.Service;

public class TransferService
{
    public decimal CalculateFee(decimal amount, bool sameCustomer, decimal feePercent)
    {
        if (sameCustomer) return 0m;
        return Math.Round(amount * feePercent / 100m, 2, MidpointRounding.AwayFromZero);
    }
}
