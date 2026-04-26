using AyvalikBankLA.Api.Config;
using AyvalikBankLA.Api.Repository;
using AyvalikBankLA.Api.Service;
using AyvalikBankLA.Api.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// EF Core
builder.Services.AddDbContext<BankDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Services (fat services — direct deps on DbContext)
builder.Services.AddScoped<PasswordValidationService>();
builder.Services.AddScoped<TransferService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<AccountService>();

// Auth
builder.Services.AddAuthentication(BasicAuthHandler.SchemeName)
    .AddScheme<BasicAuthOptions, BasicAuthHandler>(BasicAuthHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

// MVC + global exception handler
builder.Services.AddControllers();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Run admin seed on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BankDbContext>();
    await AdminSeeder.SeedAsync(db);
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Expose Program for WebApplicationFactory in tests
public partial class Program { }
