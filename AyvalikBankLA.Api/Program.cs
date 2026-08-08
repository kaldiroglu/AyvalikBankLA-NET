using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
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
// Enums travel as strings ("USD", "PREMIUM"), matching the Java and Python implementations and
// the documented API. System.Text.Json otherwise expects numeric enum values on the way IN while
// the response DTOs already emit strings - an asymmetry that made this API unusable by any client
// written against the others. Pinned by AyvalikBankContractTests.
// Browsable API docs at /swagger. The Basic scheme is declared so the "Authorize"
// button works and endpoints can be tried from the page itself.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Ayvalık Bank LA-NET", Version = "v1" });
    o.AddSecurityDefinition("basic", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "basic",
        Description = "Seeded admin: admin@ayvalikbank.dev / Admin@123!",
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "basic" },
        }] = Array.Empty<string>(),
    });
});

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
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
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Expose Program for WebApplicationFactory in tests
public partial class Program { }
