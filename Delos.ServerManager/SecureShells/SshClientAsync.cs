using System.Collections.Concurrent;
using JetBrains.Annotations;
using Renci.SshNet;

namespace Delos.ServerManager.SecureShells;

[PublicAPI]
public class SshClientAsync : IAsyncDisposable
{
    private readonly SshClient _sshClient;
    private readonly BlockingCollection<SshAction> _queue = new(new ConcurrentQueue<SshAction>());
    private Task? _thread;
    private CancellationTokenSource? _cancellationSource;

    private abstract class SshAction
    {
        private readonly CancellationToken _cancellationToken;
        private readonly TaskCompletionSource _taskCompletionSource = new();

        protected SshAction(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        public Task Task => _taskCompletionSource.Task;

        public void Process(SshClient ssh, CancellationToken cancellationToken)
        {
            var token = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationToken).Token;
            try
            {
                Run(ssh, token);
                _taskCompletionSource.SetResult();
            }
            catch (TaskCanceledException)
            {
                _taskCompletionSource.SetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                _taskCompletionSource.SetException(ex);
            }
        }

        protected abstract void Run(SshClient ssh, CancellationToken cancellationToken);
    }

    private class SshConnectAction : SshAction
    {
        protected override void Run(SshClient ssh, CancellationToken cancellationToken)
        {
            ssh.Connect();
        }

        public SshConnectAction(CancellationToken cancellationToken) : base(cancellationToken)
        {
        }
    }

    private class SshDisconnectAction : SshAction
    {
        protected override void Run(SshClient ssh, CancellationToken cancellationToken)
        {
            ssh.Disconnect();
        }

        public SshDisconnectAction(CancellationToken cancellationToken) : base(cancellationToken)
        {
        }
    }

    private class SshCommandAction : SshAction
    {
        private readonly string _action;
        public CommandResult Result { get; private set; } = null!;
        
        public SshCommandAction(string action, CancellationToken cancellationToken) : base(cancellationToken)
        {
            _action = action;
        }

        protected override void Run(SshClient ssh, CancellationToken cancellationToken)
        {
            var command = ssh.CreateCommand(_action);
            var registration = cancellationToken.Register(() => command.CancelAsync());
            try
            {
                command.Execute();
                Result = new CommandResult(command);
            }
            finally
            {
                registration.Dispose();
            }
        }
    }

    public SshClientAsync(SshClient sshClient)
    {
        _sshClient = sshClient;
    }
    
    public SshClientAsync(ConnectionInfo connectionInfo)
    {
        _sshClient = new SshClient(connectionInfo);
    }

    private void ThreadProc(SshClient ssh, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var action = _queue.Take(cancellationToken);
                action.Process(_sshClient, cancellationToken);
                if (action is SshDisconnectAction)
                    break;
            }
        }
        catch (TaskCanceledException) {}
        finally
        {
            ssh.Dispose();
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_sshClient.IsConnected)
            throw new Exception("Connection is already established");
        
        _cancellationSource = new CancellationTokenSource();
        _thread = Task.Run(() => ThreadProc(_sshClient, _cancellationSource.Token));

        var action = new SshConnectAction(cancellationToken);
        _queue.Add(action, cancellationToken);
        await action.Task;
    }

    public async Task DisconnectAsync()
    {
        var action = new SshDisconnectAction(default);
        _queue.Add(action);
        await action.Task;
        
        if (_thread != null)
            await _thread;
        _thread = null;
        
        _queue.CompleteAdding();
        // Ask thread to shutdown
        _cancellationSource?.Cancel();
    }
    
    public async Task<CommandResult> SendCommandAsync(string commandText, CancellationToken cancellationToken = default)
    {
        var action = new SshCommandAction(commandText, cancellationToken);
        _queue.Add(action, cancellationToken);
        await action.Task;
        return action.Result;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}

[PublicAPI]
public record CommandResult(int ExitCode, string Result, string Error)
{
    public CommandResult(SshCommand command) : this(command.ExitStatus, command.Result, command.Error) {}
};