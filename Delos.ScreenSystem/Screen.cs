using JetBrains.Annotations;

namespace Delos.ScreenSystem;

internal class NoScreen : Screen
{
    public override Task RunAsync() =>  Task.CompletedTask;
}

[PublicAPI]
public abstract class Screen<TScreenState> : Screen where TScreenState : class
{
    public TScreenState State => (TScreenState)ScreenState!;
}

[PublicAPI]
public abstract class Screen
{
    protected internal object? ScreenState;
    
    // ReSharper disable once MemberCanBePrivate.Global
    protected IScreenManager ScreenManager { get; private set; } = null!;

    internal async Task StartupAsyncInternal(IScreenManager screenManager, object? state)
    {
        ScreenState = state;
        ScreenManager = screenManager;
        await StartupAsync();
    }

    internal async Task ShutdownAsyncInternal()
    {
        await ShutdownAsync();
    }

    protected virtual Task StartupAsync() => Task.CompletedTask;
    protected virtual Task ShutdownAsync() => Task.CompletedTask;

    internal async Task RunAsyncInternal()
    {
        await RunAsync();
    }
    
    public abstract Task RunAsync();
}