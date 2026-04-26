using AyvalikBankLA.Api.Model;
using Microsoft.EntityFrameworkCore;

namespace AyvalikBankLA.Api.Repository;

public class BankDbContext : DbContext
{
    public BankDbContext(DbContextOptions<BankDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Settings> Settings => Set<Settings>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Customer>(b =>
        {
            b.ToTable("customers");
            b.HasKey(c => c.Id);
            b.HasIndex(c => c.Email).IsUnique();
            b.Property(c => c.Email).IsRequired();
            b.Property(c => c.Name).IsRequired();
            b.Property(c => c.Role).IsRequired();
            b.Property(c => c.CurrentPassword).IsRequired();
        });

        mb.Entity<Account>(b =>
        {
            b.ToTable("accounts");
            b.HasKey(a => a.Id);
            b.Property(a => a.Currency).HasConversion<string>().HasMaxLength(8);
            b.Property(a => a.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(a => a.Balance).HasColumnType("numeric(19,2)");
        });

        mb.Entity<Transaction>(b =>
        {
            b.ToTable("transactions");
            b.HasKey(t => t.Id);
            b.Property(t => t.Type).HasConversion<string>().HasMaxLength(16);
            b.Property(t => t.Currency).HasConversion<string>().HasMaxLength(8);
            b.Property(t => t.Amount).HasColumnType("numeric(19,2)");
        });

        mb.Entity<Settings>(b =>
        {
            b.ToTable("settings");
            b.HasKey(s => s.Key);
        });
    }
}
