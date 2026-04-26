using AyvalikBankLA.Api.Model;
using AyvalikBankLA.Api.Repository;
using Microsoft.EntityFrameworkCore;

namespace AyvalikBankLA.Api.Config;

public static class AdminSeeder
{
    public const string AdminEmail = "admin@ayvalikbank.dev";
    public const string AdminPassword = "Admin@123!";

    public static async Task SeedAsync(BankDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (!await db.Customers.AnyAsync(c => c.Email == AdminEmail))
        {
            db.Customers.Add(new Customer
            {
                Id = Guid.NewGuid(),
                Name = "System Admin",
                Email = AdminEmail,
                Role = "ADMIN",
                Tier = CustomerTier.STANDARD,
                CurrentPassword = BCrypt.Net.BCrypt.HashPassword(AdminPassword, workFactor: 12)
            });
        }
        if (!await db.Settings.AnyAsync(s => s.Key == "TRANSFER_FEE_PERCENT"))
        {
            db.Settings.Add(new Settings { Key = "TRANSFER_FEE_PERCENT", Value = "1.0" });
        }
        await db.SaveChangesAsync();
    }
}
