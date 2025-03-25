using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PomogatorBot.Web.Commands.Common;
using PomogatorBot.Web.Infrastructure;
using PomogatorBot.Web.Middlewares;
using PomogatorBot.Web.Services;
using Serilog;
using Serilog.Events;
using Telegram.Bot;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Mvc", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Warning)
    .CreateLogger();

try
{
    Log.Information("Starting web application");

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSerilog();

    builder.Services.AddPooledDbContextFactory<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
            .UseSnakeCaseNamingConvention());

    builder.Services.AddScoped(provider => provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

    builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(builder.Configuration.GetValue<string>("BotConfiguration:Token")!));

    builder.Services.AddHostedService<BotBackgroundService>();

    builder.Services
        .AddBotCommandHandlers(typeof(Program).Assembly)
        .AddScoped<IKeyboardFactory, KeyboardFactory>()
        .AddScoped<IUserService, UserService>()
        .AddScoped<CommandRouter>()
        ;

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
    builder.Services.AddHttpContextAccessor();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.UseFastEndpoints();

    app.MapGet("/", async context =>
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(Path.Combine("wwwroot", "index.html"));
    });

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
