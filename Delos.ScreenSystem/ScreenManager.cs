using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Delos.ScreenSystem;

[PublicAPI]
public interface IScreenManager
{
    Screen CurrentScreen { get; }
    
    Task PushAsync<[MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)] TScreen>() where TScreen : Screen;

    Task<TScreenState> PushAsync<[MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]TScreen, [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]TScreenState>(TScreenState state)
        where TScreen : Screen where TScreenState : class;
}

[PublicAPI]
public class ScreenManager : IScreenManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Stack<Screen> _breadcrumbs = new();
    public Screen CurrentScreen { get; private set; } = new NoScreen();
    public IEnumerable<Screen> Breadcrumbs => _breadcrumbs.ToArray();

    public ScreenManager(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private async Task<Screen> GetScreen(IServiceProvider scopeServiceProvider, Type screenType, object? state)
    {
        var screen = (Screen)scopeServiceProvider.GetRequiredService(screenType);
        await screen.StartupAsyncInternal(this, state);
        return screen;
    }

    public async Task PushAsync<TScreen>() where TScreen : Screen
    {
        await PushAsync(typeof(TScreen), null);
    }

    public async Task<TScreenState> PushAsync<TScreen, TScreenState>(TScreenState state)
        where TScreen : Screen where TScreenState : class
    {
        await PushAsync(typeof(TScreen), state);
        return state;
    }
    
    private async Task PushAsync(Type screenType, object? state)
    {
        using var scope = _serviceProvider.CreateScope();
        var lastScreen = CurrentScreen;
        var screen = await GetScreen(scope.ServiceProvider, screenType, state);
        try
        {
            CurrentScreen = screen;
            _breadcrumbs.Push(screen);
            await screen.RunAsyncInternal();
            await screen.ShutdownAsyncInternal();
        }
        finally
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            if (screen is IDisposable disposable)
                disposable.Dispose();
        }

        _breadcrumbs.Pop();
        CurrentScreen = lastScreen;
    }
}