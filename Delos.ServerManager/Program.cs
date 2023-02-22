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

    configuration["Data:ConnectionString"] ??= "Filename=delos.db;Connection=shared";
    
    var services = new ServiceCollection();
    services.AddSingleton<IScreenManager, ScreenManager>();
    services.AddSingleton<DataContext>();
    services.Configure<DataContextOptions>(configuration.GetSection(DataContextOptions.SectionName));
    
    services.AddScoped<SecureShellManager>();
    
    services.AddScoped<DashboardScreen>();
    services.AddScoped<SecureShellScreen>();
    services.AddScoped<SecureShellAddScreen>();
    services.AddScoped<SecureShellEditScreen>();
    services.AddScoped<PrivateKeyScreen>();
    services.AddScoped<PrivateKeyEditScreen>();
    

    return services.BuildServiceProvider();
}
var serviceProvider = BuildServices();

var screenManager = serviceProvider.GetRequiredService<IScreenManager>();

await screenManager.PushAsync<DashboardScreen>();

Environment.Exit(0);
