using System.Reflection;

namespace PomogatorBot.Web.CallbackQueries.Common;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddBotCallbackQueryHandlers(this IServiceCollection services, Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(x => x is { IsClass: true, IsAbstract: false } && x.IsAssignableTo(typeof(ICallbackQueryHandler)));

        foreach (var type in handlerTypes)
        {
            services.AddScoped(typeof(ICallbackQueryHandler), type);
        }

        return services;
    }
}
