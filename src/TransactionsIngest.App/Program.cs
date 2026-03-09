using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TransactionsIngest.App.Data;
using TransactionsIngest.App.Options;
using TransactionsIngest.App.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.SetBasePath(AppContext.BaseDirectory);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

builder.Services
    .AddOptions<IngestionOptions>()
    .Bind(builder.Configuration.GetSection(IngestionOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddDbContext<AppDbContext>((_, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=transactions.db";
    options.UseSqlite(connectionString);
});

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ISnapshotClient, MockSnapshotClient>();
builder.Services.AddScoped<IIngestionService, IngestionService>();

var host = builder.Build();

using var scope = host.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

try
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var service = scope.ServiceProvider.GetRequiredService<IIngestionService>();
    var result = await service.RunOnceAsync();

    logger.LogInformation(
        "Run complete. inserted={Inserted} updated={Updated} revoked={Revoked} finalized={Finalized} revisions={Revisions}",
        result.Inserted,
        result.Updated,
        result.Revoked,
        result.Finalized,
        result.RevisionsWritten);
}
catch (Exception ex)
{
    logger.LogError(ex, "Ingestion run failed");
    return 1;
}

return 0;
