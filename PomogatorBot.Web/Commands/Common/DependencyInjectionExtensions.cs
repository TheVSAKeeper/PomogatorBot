using System.Reflection;

namespace PomogatorBot.Web.Commands.Common;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddBotCommandHandlers(this IServiceCollection services, Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(x => x is { IsClass: true, IsAbstract: false } && x.IsAssignableTo(typeof(IBotCommandHandler)));

        foreach (var type in handlerTypes)
        {
            services.AddScoped(typeof(IBotCommandHandler), type);

            var metadataInterface = type.GetInterfaces()
                .FirstOrDefault(x => x == typeof(ICommandMetadata));

            if (metadataInterface == null)
            {
                continue;
            }

            var property = type.GetProperty(nameof(ICommandMetadata.Metadata), BindingFlags.Public | BindingFlags.Static);

            if (property?.GetValue(null) is CommandMetadata metadata)
            {
                services.AddSingleton(metadata);
            }
        }

        return services;
    }
}
