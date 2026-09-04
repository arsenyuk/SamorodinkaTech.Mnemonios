using Mnemonios.Domain.Interfaces;
using Mnemonios.Infrastructure;
using Mnemonios.Infrastructure.Middleware;
using Mnemonios.Infrastructure.Persistence;
using Mnemonios.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using SamorodinkaTech.Mnemonios.Steward.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IPersonRepository, PersonRepository>();
builder.Services.AddScoped<IStewardService, StewardService>();
builder.Services.AddScoped<IUrlMaskService, UrlMaskService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IClientIpProvider, HttpContextIpProvider>();

builder.Services.AddRazorPages();

var app = builder.Build();

app.UseMiddleware<ExceptionLoggingMiddleware>();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
