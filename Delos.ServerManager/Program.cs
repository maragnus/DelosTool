using Delos.ScreenSystem;
using Delos.ServerManager.Data;
using Delos.ServerManager.Screens;
using Delos.ServerManager.SecureShells;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

IServiceProvider BuildServices()
{
    var configuration = new ConfigurationBuilder()
        .AddEnvironmentVariables()
        .AddJsonFile("appsettings.json", optional: true)
        .Build();

    var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    configuration["Data:ConnectionString"] ??= $"Filename={Path.Combine(homePath, ".delos.db")};Connection=shared";
    
    var services = new ServiceCollection();
    services.RegisterScreens<Program>();

    services.AddSingleton<DataContext>();
    services.Configure<DataContextOptions>(configuration.GetSection(DataContextOptions.SectionName));
    services.AddScoped<SecureShellManager>();

    return services.BuildServiceProvider();
}
var serviceProvider = BuildServices();

var screenManager = serviceProvider.GetRequiredService<IScreenManager>();

await screenManager.PushAsync<DashboardScreen>();

Environment.Exit(0);
