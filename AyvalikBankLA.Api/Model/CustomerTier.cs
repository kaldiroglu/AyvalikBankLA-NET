namespace AyvalikBankLA.Api.Model;

public enum CustomerTier { STANDARD, PREMIUM, PRIVATE }

public static class CustomerTierPolicy
{
    public static decimal FeeMultiplier(this CustomerTier t) => t switch
    {
        CustomerTier.STANDARD => 1.00m,
        CustomerTier.PREMIUM => 0.50m,
        CustomerTier.PRIVATE => 0.00m,
        _ => 1.00m
    };

    public static decimal? MaxPerTransfer(this CustomerTier t) => t switch
    {
        CustomerTier.STANDARD => 5000m,
        CustomerTier.PREMIUM => 50000m,
        CustomerTier.PRIVATE => null,
        _ => 5000m
    };

    public static decimal? MaxPerWithdrawal(this CustomerTier t) => t switch
    {
        CustomerTier.STANDARD => 5000m,
        CustomerTier.PREMIUM => 25000m,
        CustomerTier.PRIVATE => null,
        _ => 5000m
    };
}
