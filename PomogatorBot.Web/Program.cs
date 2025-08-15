using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PomogatorBot.Web.CallbackQueries.Common;
using PomogatorBot.Web.Common.Configuration;
using PomogatorBot.Web.Common.Keyboard;
using PomogatorBot.Web.Infrastructure;
using PomogatorBot.Web.Middlewares;
using PomogatorBot.Web.Services.ExternalClients;
using Serilog;
using Serilog.Events;
using Telegram.Bot;

var logPath = Path.Combine(Environment.ProcessPath ?? Environment.CurrentDirectory, "logs", "verbose.log");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Mvc", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(logPath,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}]({SourceContext}) {Message:lj}{NewLine}{Exception}",
        rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Запуск веб-приложения");

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSerilog();
    builder.Services.AddHealthChecks();

    builder.Services.Configure<BotConfiguration>(builder.Configuration.GetSection("BotConfiguration"));
    builder.Services.Configure<AdminConfiguration>(builder.Configuration.GetSection("Admin"));

    builder.Services.AddPooledDbContextFactory<ApplicationDbContext>(options =>
        options.UseNpgsql(new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("DefaultConnection"))
                .EnableDynamicJson()
                .Build())
            .UseSnakeCaseNamingConvention());

    builder.Services.AddScoped(provider => provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

    var botConfiguration = builder.Configuration.GetSection("BotConfiguration").Get<BotConfiguration>();

    if (string.IsNullOrEmpty(botConfiguration?.Token))
    {
        throw new InvalidOperationException("Токен бота не настроен. Пожалуйста, установите «BotConfiguration:Token» в конфигурации.");
    }

    builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botConfiguration.Token));

    builder.Services.AddHostedService<BotBackgroundService>();

    builder.Services.AddSingleton<BroadcastExecutionService>();
    builder.Services.AddHostedService(provider => provider.GetRequiredService<BroadcastExecutionService>());

    builder.Services.AddSingleton<BroadcastNotificationService>();
    builder.Services.AddHostedService(provider => provider.GetRequiredService<BroadcastNotificationService>());

    builder.Services.AddBotCommandHandlers(typeof(Program).Assembly)
        .AddScoped<CommandRouter>();

    builder.Services.AddBotCallbackQueryHandlers(typeof(Program).Assembly)
        .AddScoped<CallbackQueryRouter>();

    builder.Services.AddScoped<KeyboardFactory>()
        .AddScoped<UserService>()
        .AddScoped<MessagePreviewService>()
        .AddScoped<MessageTemplateService>()
        .AddScoped<BroadcastHistoryService>()
        .AddScoped<ExternalClientService>()
        .AddSingleton<BroadcastPendingService>()
        .AddSingleton<BroadcastProgressService>();

    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
            context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
        };
    });

    builder.Services.AddExceptionHandler<ExceptionHandler>();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddFastEndpoints();

    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = ApiKeyAuthenticationHandler.SchemeName;
            options.DefaultChallengeScheme = ApiKeyAuthenticationHandler.SchemeName;
        })
        .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });

    builder.Services.AddAuthorization();
    builder.Services.AddHttpContextAccessor();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseFastEndpoints();

    app.MapHealthChecks("/live");

    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var maxRetries = 5;
        var retryCount = 0;
        var delay = TimeSpan.FromSeconds(1);

        while (true)
        {
            try
            {
                var dbContext = services.GetRequiredService<ApplicationDbContext>();
                await dbContext.Database.MigrateAsync();
                Log.Information("Промигрировано");
                break;
            }
            catch (Exception exception)
            {
                retryCount++;

                if (retryCount >= maxRetries)
                {
                    throw new InvalidOperationException($"Миграция базы данных не удалась после {retryCount} попыток", exception);
                }

                Log.Warning(exception, "Попытка миграции {RetryCount} не удалась, повторная попытка через {Delay} сек", retryCount, delay.TotalSeconds);
                await Task.Delay(delay);
                delay *= 2;
            }
        }
    }

    await app.RunAsync();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Непредвиденная остановка приложения");
}
finally
{
    await Log.CloseAndFlushAsync();
}
