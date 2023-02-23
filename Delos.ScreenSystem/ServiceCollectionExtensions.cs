using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Delos.ScreenSystem;

public static class ServiceCollectionExtensions
{
    [PublicAPI]
    public static IServiceCollection RegisterScreens<TAssembly>(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IScreenManager, ScreenManager>();

        var screenTypes = typeof(TAssembly).Assembly.GetTypes()
            .Where(x => x.IsAssignableTo(typeof(Screen)) && !x.IsAbstract);

        foreach (var screenType in screenTypes)
            serviceCollection.AddScoped(screenType);
        
        return serviceCollection;
    }
    
}