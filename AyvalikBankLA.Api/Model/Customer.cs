namespace AyvalikBankLA.Api.Model;

// Anemic JPA-equivalent entity. All business logic lives in the service layer.
public class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "CUSTOMER";
    public string CurrentPassword { get; set; } = "";
}
