using Mnemonios.Api.Endpoints;
using Mnemonios.Domain.Interfaces;
using Mnemonios.Infrastructure;
using Mnemonios.Infrastructure.Middleware;
using Mnemonios.Infrastructure.Persistence;
using Mnemonios.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
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

builder.Services.Configure<HmacSettings>(builder.Configuration.GetSection("HmacSettings"));

builder.Services.AddScoped<INormalizationService, NormalizationService>();
builder.Services.AddScoped<IIdentificationKeyService, IdentificationKeyService>();
builder.Services.AddScoped<IPersonRepository, PersonRepository>();
builder.Services.AddScoped<IPersonResolveService, PersonResolveService>();
builder.Services.AddScoped<IPersonCessationService, PersonCessationService>();
builder.Services.AddScoped<IPersonMergeService, PersonMergeService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IClientIpProvider, HttpContextIpProvider>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<ExceptionLoggingMiddleware>();
app.MapPersonEndpoints();
app.Run();
