using Microsoft.EntityFrameworkCore;
using PomogatorBot.Web;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextPool<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddPooledDbContextFactory<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(builder.Configuration.GetValue<string>("BotConfiguration:Token")!));
builder.Services.AddHostedService<BotBackgroundService>();

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseStaticFiles();

app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine("wwwroot", "index.html"));
});

app.MapPost("/notify", async (NotifyRequest request, ApplicationDbContext dbContext, ITelegramBotClient botClient) =>
{
    var users = await dbContext.Users.ToListAsync();

    foreach (var user in users)
    {
        var messageText = request.Message
            .Replace("<first_name>", user.FirstName)
            .Replace("<username>", user.Username);

        try
        {
            await botClient.SendMessage(user.UserId, messageText);
        }
        catch (Exception exception)
        {
            app.Logger.LogError(exception, "Error sending message to user {UserId}", user.UserId);
        }
    }

    return Results.Ok();
});

app.Run();

public record NotifyRequest(string Message);
