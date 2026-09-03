using Mnemonios.Domain.Interfaces;
using Mnemonios.Infrastructure.Persistence;
using Mnemonios.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using SamorodinkaTech.Mnemonios.Steward.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.Configure<HmacSettings>(builder.Configuration.GetSection("HmacSettings"));

builder.Services.AddScoped<INormalizationService, NormalizationService>();
builder.Services.AddScoped<IIdentificationKeyService, IdentificationKeyService>();
builder.Services.AddScoped<IPersonRepository, PersonRepository>();
builder.Services.AddScoped<IPersonMergeService, PersonMergeService>();
builder.Services.AddScoped<IStewardService, StewardService>();

builder.Services.AddRazorPages();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
