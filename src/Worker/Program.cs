using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mnemonios.Domain.Interfaces;
using Mnemonios.Infrastructure.Persistence;
using Mnemonios.Infrastructure.Services;
using Mnemonios.Worker.Configuration;
using Mnemonios.Worker.Scheduling;
using Mnemonios.Worker.Tasks;

var builder = Host.CreateApplicationBuilder(args);

// --- База данных ---
var connectionString = builder.Configuration["ConnectionStrings:DefaultConnection"]
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' не найдена.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- Конфигурация ---
builder.Services.Configure<HmacSettings>(builder.Configuration.GetSection("HmacSettings"));
builder.Services.Configure<WorkerConfig>(builder.Configuration.GetSection("Worker"));

// --- Сервисы домена ---
builder.Services.AddScoped<INormalizationService, NormalizationService>();
builder.Services.AddScoped<IIdentificationKeyService, IdentificationKeyService>();
builder.Services.AddScoped<IPersonRepository, PersonRepository>();
builder.Services.AddScoped<IPersonResolveService, PersonResolveService>();
builder.Services.AddScoped<IPersonCessationService, PersonCessationService>();

// --- Задачи планировщика (keyed services) ---
builder.Services.AddKeyedScoped<IWorkerTask, ReconcileCessationsTask>("reconcile-cessations");

// --- Планировщик ---
builder.Services.AddHostedService<WorkerTaskScheduler>();

var host = builder.Build();
await host.RunAsync();
