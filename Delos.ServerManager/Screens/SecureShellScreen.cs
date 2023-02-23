using System.Text;
using Delos.ScreenSystem;
using Delos.SecureShells;
using JetBrains.Annotations;
using Spectre.Console;

namespace Delos.ServerManager.Screens;

[PublicAPI]
public class SecureShellScreenState
{
    public bool IsNew { get; set; }
    public string Name { get; set; } = null!;
    public string? Host { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? RootPassword { get; set; }
    public string[]? KeyNames { get; set; }
    public string? MachineInfo { get; set; }

    public static SecureShellScreenState FromProfile(SecureShellSecureProfile profile) =>
        new()
        {
            IsNew = profile.Id == null,
            Name = profile.Name,
            Host = profile.Host,
            UserName = profile.UserName,
            Password = profile.Password,
            RootPassword = profile.RootPassword,
            KeyNames = profile.KeyPairNames,
            MachineInfo = profile.MachineInfo
        };

    public SecureShellSecureProfile ToProfile() =>
        new(null, Name, Host, UserName, Password, RootPassword, KeyNames, null)
        {
            MachineInfo = MachineInfo
        };
}

public class SecureShellEditScreen : Screen<SecureShellScreenState>
{
    private readonly SecureShellManager _sshManager;
    private string _originalName = null!;

    public SecureShellEditScreen(SecureShellManager sshManager)
    {
        _sshManager = sshManager;
    }

    protected override Task StartupAsync()
    {
        _originalName = State.Name;
        return Task.CompletedTask;
    }

    public override async Task RunAsync()
    {
        while (true)
        {
            var infoText = string.IsNullOrWhiteSpace(State.MachineInfo)
                ? "<run test to populate>"
                : State.MachineInfo;
            
            AnsiConsole.Clear();
            var name = new Option($"Name: [blue]{State.Name}[/]");
            var host = new Option($"Host: [blue]{State.Host}[/]");
            var user = new Option($"User: [blue]{State.UserName}[/]");
            var pass = new Option($"Password: [blue]{State.Password}[/]");
            var root = new Option($"Root Password: [blue]{State.RootPassword}[/]");
            var keys = new Option($"Private Keys: [blue]{string.Join(",", State.KeyNames ?? new[] { "<none>" })}[/]");
            var info = new Option($"Info: [blue]{infoText}[/]");
            var test = new Option("[purple]Test[/]...");
            var save = State.IsNew ? new Option("[green]Add[/]...") : new Option("[green]Save[/]...");
            var delete = new Option("[red]Delete[/]...");
            var cancel = new Option("[yellow]Cancel[/]");

            var prompt = new SelectionPrompt<Option>().WrapAround();
            if (State.IsNew)
                prompt.AddChoices(name, host, user, pass, root, keys, info, test, save, cancel);
            else
                prompt.AddChoices(name, host, user, pass, root, keys, info, test, save, delete, cancel);

            var response = AnsiConsole.Prompt(prompt);

            if (response == cancel) return;
            if (response == save)
            {
                await Store();
                return;
            }

            if (response == test)
                await Test();

            if (response == delete)
            {
                if (AnsiConsole.Confirm("Are you sure you want to [red]delete[/] this profile?", false))
                {
                    await _sshManager.SecureShellStore.Delete(_originalName);
                    return;
                }
            }

            if (response == name)
            {
                var value = AnsiConsole.Prompt(
                    new TextPrompt<string>("Rename this profile to?")
                        .ValidationErrorMessage("[red]Invalid profile name[/]")
                        .Validate(value => SecureShellProfile.IsNameValid(value)
                            ? ValidationResult.Success()
                            : ValidationResult.Error("[red]Must be 2+ alphanumerics and start with alpha[/]")));

                if (await _sshManager.SecureShellStore.Get(value) != null)
                {
                    var replace =
                        AnsiConsole.Confirm("A profile with this names exists, would you like to replace it?");
                    if (!replace) continue;
                }

                State.Name = value;
            }

            if (response == host)
            {
                State.Host = AnsiConsole.Prompt(
                    new TextPrompt<string>("Host (IP Address, Hostname, optionally Port):")
                        .AllowEmpty()
                        .Validate(value =>
                        {
                            if (!SecureShellProfile.IsHostValid(value))
                                return ValidationResult.Error(
                                    "[red]Must be a valid hostname or ip address, optionally with port[/]");
                            return ValidationResult.Success();
                        }));
            }

            if (response == user)
            {
                State.UserName = AnsiConsole.Prompt(
                    new TextPrompt<string>($"User name:")
                        .AllowEmpty());
            }

            if (response == pass)
            {
                State.Password = AnsiConsole.Prompt(
                    new TextPrompt<string>($"User password for [blue]{State.UserName ?? "sign in user"}[/]:")
                        .AllowEmpty().Secret());
            }

            if (response == root)
            {
                State.RootPassword = AnsiConsole.Prompt(
                    new TextPrompt<string>($"User password for [blue]root[/] (for sudo):")
                        .AllowEmpty().Secret());
            }

            if (response == keys)
            {
                const string addItem = "[green]New...[/]";
                var keyNames = (await _sshManager.PrivateKeyStore.Get()).Select(x => x.Name).ToArray();
                var select = new MultiSelectionPrompt<string>()
                    .Title("Select the [blue]keys[/] to use with this connection:")
                    .NotRequired()
                    .AddChoices(keyNames.Concat(new[] { addItem }));
                
                foreach (var keyName in State.KeyNames ?? Array.Empty<string>())
                    select.Select(keyName);
                
                var selection = AnsiConsole.Prompt(select).ToHashSet();
                if (selection.Contains(addItem))
                {
                    selection.Remove(addItem);
                    
                    while (true)
                    {
                        var newKeyName = AnsiConsole.Ask<string>("Name for this key:").Trim();
                        if (!PrivateKeyProfile.IsNameValid(newKeyName))
                        {
                            AnsiConsole.MarkupLine("[red]Must be 2+ alphanumerics and start with alpha[/]");
                            continue;
                        }
                        await _sshManager.PrivateKeyStore.StoreNew(newKeyName);
                        selection.Add(newKeyName);
                        break;
                    }
                    
                }

                State.KeyNames = selection.ToArray();
            }
        }
    }

    private async Task Test()
    {
        async Task<string> RunCommand(SshClientAsync ssh, string command)
        {
            AnsiConsole.MarkupLine("[white]Executing:[/] {0}", command);
            var response = await ssh.SendCommandAsync(command);
            if (!string.IsNullOrWhiteSpace(response.Result))
                AnsiConsole.MarkupLine("{0}", response.Result.Trim());
            if (!string.IsNullOrWhiteSpace(response.Error))
                AnsiConsole.MarkupLine("[maroon]{0}[/]", response.Error.Trim());
            AnsiConsole.MarkupLine("[white]Completed with exit code {0}[/]", response.ExitCode);

            if (response.ExitCode != 0) throw new Exception($"Command failed: {command}");
            
            return response.Result;
        }
        
        try
        {
            // Construct it manually since it isn't saved yet
            var secureShell = new SecureShell(State.ToProfile(), _sshManager);
            await using var ssh = await secureShell.ConnectAsync();
            
            State.MachineInfo = await RunCommand(ssh, "uname -a");

            // TODO -- check root via sudo whoami
            
            AnsiConsole.MarkupLine("Test has [green]completed[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            AnsiConsole.MarkupLine("Test has [red]failed[/]");
        }
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Press [blue]ENTER[/] to continue");
        Console.ReadLine();
    }

    private async Task Store()
    {
        if (_originalName != State.Name)
            await _sshManager.SecureShellStore.Rename(_originalName, State.Name);

        var profile = State.ToProfile();
        await _sshManager.SecureShellStore.Store(profile);
    }
}

public class SecureShellAddScreen : Screen
{
    private readonly SecureShellManager _sshManager;

    public SecureShellAddScreen(SecureShellManager sshManager)
    {
        _sshManager = sshManager;
    }

    public override async Task RunAsync()
    {
        while (true)
        {
            var name = AnsiConsole.Prompt(
                new TextPrompt<string>("What would you like to [blue]name[/] this connection for your reference?")
                    .ValidationErrorMessage("[red]Invalid profile name[/]")
                    .Validate(value => SecureShellProfile.IsNameValid(value)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("[red]Must be 2+ alphanumerics and start with alpha[/]")));

            var profile = await _sshManager.SecureShellStore.Get(name);
            if (profile != null)
            {
                var response =
                    AnsiConsole.Confirm(
                        "A profile with this [blue]name[/] exists, would you like to [green]edit[/] it instead?");
                if (!response)
                    continue;
                await ScreenManager.PushAsync<SecureShellEditScreen, SecureShellScreenState>(
                    SecureShellScreenState.FromProfile(profile));
                return;
            }

            var state = new SecureShellScreenState() { IsNew = true, Name = name };
            await ScreenManager.PushAsync<SecureShellEditScreen, SecureShellScreenState>(state);

            return;
        }
    }
}

public class SecureShellScreen : Screen
{
    private readonly SecureShellManager _sshManager;

    public SecureShellScreen(SecureShellManager sshManager)
    {
        _sshManager = sshManager;
    }

    public override async Task RunAsync()
    {
        while (true)
        {
            var profiles = await _sshManager.SecureShellStore.Get();
            RenderTable(profiles);

            var editOption = new Option("Edit...");
            var addOption = new Option("Add...");
            var ppkOption = new Option("Manage Private Keys...");
            var returnOption = new Option("[yellow]Return[/]");
            var options = profiles.Length > 0
                ? new[] { editOption, addOption, ppkOption, returnOption }
                : new[] { addOption, ppkOption, returnOption };

            var selection = AnsiConsole.Prompt(new SelectionPrompt<Option>()
                .Title("What would you like to do with SSH profiles?")
                .AddChoices(options));

            if (selection == returnOption)
                return;

            if (selection == addOption)
                await ScreenManager.PushAsync<SecureShellAddScreen>();

            if (selection == editOption)
                await RenderEditOptionList(profiles);

            if (selection == ppkOption)
                await ScreenManager.PushAsync<PrivateKeyScreen>();
        }
    }

    private async Task RenderEditOptionList(SecureShellSecureProfile[] profiles)
    {
        const string returnOption = "[yellow]Return[/]";

        var name = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .AddChoices(profiles.Select(x => x.Name).Concat(new[] { returnOption })));

        if (name == returnOption) return;

        var profile = profiles.Single(x => x.Name == name);

        var state = SecureShellScreenState.FromProfile(profile);

        await ScreenManager.PushAsync<SecureShellEditScreen, SecureShellScreenState>(state);
    }

    private static void RenderTable(IEnumerable<SecureShellSecureProfile> profiles)
    {
        var table = new Table()
            .AddColumn("Name")
            .AddColumn("Host")
            .AddColumn("User")
            .AddColumn("Auth");

        var authType = new StringBuilder();
        foreach (var profile in profiles)
        {
            authType.Clear();
            if (!string.IsNullOrWhiteSpace(profile.UserName)) authType.Append("User,");
            if (!string.IsNullOrWhiteSpace(profile.Password)) authType.Append("Pass,");
            if (profile.KeyPairNames?.Length > 0) authType.Append("Key,");
            if (authType.Length > 0)
                authType.Remove(authType.Length - 1, 1);
            else
                authType.Append("None");
            table.AddRow(profile.Name, profile.Host ?? "<none>", profile.UserName ?? "<none>", authType.ToString());
        }

        AnsiConsole.Write(table);
    }
}