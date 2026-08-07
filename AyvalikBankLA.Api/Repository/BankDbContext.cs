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

    /// <summary>
    /// Bumps the optimistic-lock token on every modified account.
    ///
    /// <para>Centralised here rather than in each service method because AccountService writes
    /// accounts from a dozen places; a token that one of them forgets to increment is a guard
    /// that silently does nothing. Mirrors AyvalikBankHA-JAVA Refactorings.md entry 5.</para>
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Account>())
            if (entry.State == EntityState.Modified)
                entry.Entity.Version++;
        return base.SaveChangesAsync(cancellationToken);
    }

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
            b.Property(c => c.Tier).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(c => c.CurrentPassword).IsRequired();
        });

        mb.Entity<Account>(b =>
        {
            b.ToTable("accounts");
            b.HasKey(a => a.Id);
            b.Property(a => a.Currency).HasConversion<string>().HasMaxLength(8);
            b.Property(a => a.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(a => a.Type).HasConversion<string>().HasMaxLength(16);
            b.Property(a => a.Balance).HasColumnType("numeric(19,2)");
            b.Property(a => a.OverdraftLimit).HasColumnType("numeric(19,2)");
            b.Property(a => a.InterestRate).HasColumnType("numeric(10,6)");
            b.Property(a => a.Principal).HasColumnType("numeric(19,2)");
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
