using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Mnemonios.Infrastructure.Persistence;
using Npgsql;
using Serilog;
using Serilog.Events;

namespace Mnemonios.IntegrationTests;

/// <summary>
/// WebApplicationFactory: очистка БД через TRUNCATE, изоляция логирования тестов.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private DbConnection? _connection;
    private bool _disposed;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var logDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "logs");
        Directory.CreateDirectory(logDir);

        builder.UseSerilog((context, lc) =>
        {
            lc.MinimumLevel.Information()
              .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
              .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
              .WriteTo.Console(
                outputTemplate: "[TEST] {Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}")
              .WriteTo.File(
                path: Path.Combine(logDir, "integration-.log"),
                rollingInterval: RollingInterval.Hour,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
        });

        builder.ConfigureWebHost(webBuilder =>
        {
            webBuilder.ConfigureServices(services =>
            {
                var descriptors = services.Where(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                      || d.ServiceType == typeof(AppDbContext))
                    .ToList();

                foreach (var descriptor in descriptors)
                    services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(GetConnectionString()));
            });
        });

        return base.CreateHost(builder);
    }

    public async Task InitializeAsync()
    {
        _connection = new NpgsqlConnection(GetConnectionString());
        await _connection.OpenAsync();
        await ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
    }

    private async Task ResetDatabaseAsync()
    {
        if (_connection is null) return;

        var command = _connection.CreateCommand();
        command.CommandText = @"
            TRUNCATE TABLE ext_person_defects CASCADE;
            TRUNCATE TABLE ext_person_cessations CASCADE;
            TRUNCATE TABLE ext_person_deferred_cessations CASCADE;
            TRUNCATE TABLE ext_persons CASCADE;
            TRUNCATE TABLE person_defects CASCADE;
            TRUNCATE TABLE person_deferred_cessations CASCADE;
            TRUNCATE TABLE person_external_ids CASCADE;
            TRUNCATE TABLE person_documents CASCADE;
            TRUNCATE TABLE person_identification_keys CASCADE;
            TRUNCATE TABLE person_review_queue CASCADE;
            TRUNCATE TABLE persons CASCADE;
        ";
        await command.ExecuteNonQueryAsync();
    }

    private static string GetConnectionString()
    {
        return "Host=localhost;Port=5432;Database=mnemonios;Username=mnemonios;Password=mnemonios_dev";
    }
}
