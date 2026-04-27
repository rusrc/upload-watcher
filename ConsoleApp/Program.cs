using FileMonitor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Services.AddSerilog();
builder.Services.Configure<MonitorSettings>(builder.Configuration.GetSection(MonitorSettings.SectionName));
builder.Services.AddHostedService<FileMonitorService>();

var host = builder.Build();

try
{
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host завершился с ошибкой.");
}
finally
{
    await Log.CloseAndFlushAsync();
}
