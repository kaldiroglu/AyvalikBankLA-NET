using AyvalikBankLA.Api.Exception;
using AyvalikBankLA.Api.Model;
using AyvalikBankLA.Api.Repository;
using Microsoft.EntityFrameworkCore;

namespace AyvalikBankLA.Api.Service;

public class CustomerService
{
    private readonly BankDbContext _db;
    private readonly PasswordValidationService _validator;

    public CustomerService(BankDbContext db, PasswordValidationService validator)
    {
        _db = db;
        _validator = validator;
    }

    public async Task<Customer> CreateCustomerAsync(string name, string email, string rawPassword)
    {
        _validator.Validate(rawPassword);
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            Role = "CUSTOMER",
            CurrentPassword = BCrypt.Net.BCrypt.HashPassword(rawPassword, workFactor: 12)
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return customer;
    }

    public async Task DeleteCustomerAsync(Guid id)
    {
        var customer = await _db.Customers.FindAsync(id)
            ?? throw new CustomerNotFoundException($"Customer not found: {id}");
        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
    }

    public Task<List<Customer>> ListCustomersAsync() =>
        _db.Customers.AsNoTracking().ToListAsync();

    public async Task ChangePasswordAsync(Guid customerId, string rawNewPassword)
    {
        _validator.Validate(rawNewPassword);
        var customer = await _db.Customers.FindAsync(customerId)
            ?? throw new CustomerNotFoundException($"Customer not found: {customerId}");
        if (BCrypt.Net.BCrypt.Verify(rawNewPassword, customer.CurrentPassword))
            throw new PasswordReusedException("New password must differ from the current one");
        customer.CurrentPassword = BCrypt.Net.BCrypt.HashPassword(rawNewPassword, workFactor: 12);
        await _db.SaveChangesAsync();
    }
}
