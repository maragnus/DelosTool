using Delos.ScreenSystem;
using Delos.SecureShells;
using Spectre.Console;

namespace Delos.ServerManager.Screens;

public class PrivateKeyEditScreen : Screen<string>
{
    private readonly SecureShellManager _sshManager;

    public PrivateKeyEditScreen(SecureShellManager sshManager)
    {
        _sshManager = sshManager;
    }

    public override async Task RunAsync()
    {
        var key = await _sshManager.PrivateKeyStore.Get(State);
        if (key == null) return;

        var export = new Option("Export...");
        var delete = new Option("[red]Delete[/]...");
        var exit = new Option("[yellow]Return[/]");

        var usedBy = await        _sshManager.PrivateKeyStore.GetSecureShellsUsing(key.Name);
        
        
        AnsiConsole.MarkupLine("Private Key: [blue]{0}[/]", key.Name);
        AnsiConsole.MarkupLine("[gray]ssh-rsa {0} {1}[/]", key.ToRfcPublicKey(), key.Name);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Used by: [blue]{0}[/]", string.Join("[/], [blue]", usedBy));
        AnsiConsole.WriteLine();
        
        var response = AnsiConsole.Prompt(new SelectionPrompt<Option>()
            .Title($"What would you like to do with [blue]{key.Name}[/]:")
            .AddChoices(export, delete, exit));

        if (response == delete)
        {
            var confirm = AnsiConsole.Confirm($"Are you sure you want to delete this key?", false);
            if (!confirm) return;
            
            await _sshManager.PrivateKeyStore.Delete(State);
        }
    }
}

public class PrivateKeyScreen : Screen
{
    private readonly SecureShellManager _sshManager;

    public PrivateKeyScreen(SecureShellManager sshManager)
    {
        _sshManager = sshManager;
    }

    public override async Task RunAsync()
    {
        AnsiConsole.Clear();
        while (true)
        {
            var keys = await _sshManager.PrivateKeyStore.Get();

            var options = keys.Select(x => new Option(x.Name));
            
            var create = new Option("Create...");
            var import = new Option("Import...");
            var exit = new Option("[yellow]Return[/]");

            var selection = AnsiConsole.Prompt(new SelectionPrompt<Option>()
                .Title("White Private Key would you like to edit?")
                .AddChoices(options.Concat(new[] { create, import, exit })));
            
            if (selection == exit) return;

            if (selection == create)
            {
                while (true)
                {
                    var name = AnsiConsole.Ask<string>("Name for this key:").Trim();
                    if (!PrivateKeyProfile.IsNameValid(name))
                    {
                        AnsiConsole.MarkupLine("[red]Must be 2+ alphanumerics and start with alpha[/]");
                        continue;
                    }
                    await _sshManager.PrivateKeyStore.StoreNew(name);
                    break;
                }
            }

            if (selection == import)
            {
                try
                {
                    var name = AnsiConsole.Ask<string>("Name for this key:").Trim();
                    var path = AnsiConsole.Ask<string>("Path of this key file:").Trim();
                    await _sshManager.PrivateKeyStore.Import(name, path);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine("[red]{0}[/]", ex.Message);
                }
            }

            await ScreenManager.PushAsync<PrivateKeyEditScreen, string>(selection.Label);
        }
    }
}