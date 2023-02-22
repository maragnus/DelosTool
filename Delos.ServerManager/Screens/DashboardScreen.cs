using Delos.ScreenSystem;
using Spectre.Console;

namespace Delos.ServerManager.Screens;

public class DashboardScreen : Screen
{
    public override async Task RunAsync()
    {
        while (true)
        {
            var sshOption = new Option("Manage SSH");
            var exitOption = new Option("[yellow]Exit[/]");

            var response = AnsiConsole.Prompt(new SelectionPrompt<Option>()
                .Title("Where would you like to go?")
                .AddChoices(sshOption, exitOption));

            if (response == sshOption)
                await ScreenManager.PushAsync<SecureShellScreen>();
            else if (response == exitOption)
                break;
        }
        
        AnsiConsole.Write(new Markup("[green]Good-bye[/]"));
    }
}