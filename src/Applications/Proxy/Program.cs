using Mnemonios.Domain.Interfaces;
using Mnemonios.Infrastructure.Services;
using Mnemonios.Proxy.Configuration;
using Mnemonios.Proxy.Endpoints;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.Configure<HmacSettings>(builder.Configuration.GetSection("HmacSettings"));
builder.Services.Configure<ProxyConfig>(builder.Configuration.GetSection("Proxy"));

builder.Services.AddScoped<INormalizationService, NormalizationService>();
builder.Services.AddScoped<IIdentificationKeyService, IdentificationKeyService>();

builder.Services.AddHttpClient("MnemoniosApi", (sp, client) =>
{
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProxyConfig>>().Value;
    client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
});

var app = builder.Build();

app.UseHttpsRedirection();
app.MapProxyEndpoints();
app.Run();
