using System.Text;
using Delos.ScreenSystem;
using Delos.ServerManager.SecureShells;
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

    public static SecureShellScreenState FromProfile(SecureShellProfileSecure profile) =>
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

    public SecureShellProfileSecure ToProfile() =>
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
            AnsiConsole.Clear();
            var name = new Option($"Name: [blue]{State.Name}[/]");
            var host = new Option($"Host: [blue]{State.Host}[/]");
            var user = new Option($"User: [blue]{State.UserName}[/]");
            var pass = new Option($"Password: [blue]{State.Password}[/]");
            var root = new Option($"Root Password: [blue]{State.RootPassword}[/]");
            var keys = new Option($"Private Keys: [blue]{string.Join(",", State.KeyNames ?? new[] { "<none>" })}[/]");
            var info = new Option($"Info: [blue]{State.MachineInfo}[/]");
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
                    await _sshManager.DeleteProfile(_originalName);
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

                if (await _sshManager.GetSecureShellProfile(value) != null)
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
                var keyNames = (await _sshManager.GetPrivateKeys()).Select(x => x.Name).ToArray();
                var select = new MultiSelectionPrompt<string>()
                    .Title("Select the [blue]keys[/] to use with this connection:")
                    .NotRequired()
                    .AddChoices(keyNames);
                
                foreach (var keyName in State.KeyNames ?? Array.Empty<string>())
                    select.Select(keyName);
                
                State.KeyNames = AnsiConsole.Prompt(select).ToArray();
            }
        }
    }

    private async Task Test()
    {
        var secureShell = new SecureShell(State.ToProfile(), _sshManager);
        var ssh = await secureShell.ConnectAsync();
        var response = await ssh.SendCommandAsync("uname -a");
        if (response.ExitCode != 0)
        {
            AnsiConsole.MarkupLine("[red]{0}[/]", response.Error);
            if (!AnsiConsole.Confirm("Continue?")) return;
        }
        State.MachineInfo = response.Result;
        
        // TODO -- check root via sudo whoami
        AnsiConsole.WriteLine(response.Result);
    }

    private async Task Store()
    {
        if (_originalName != State.Name)
            await _sshManager.RenameSecureShellProfile(_originalName, State.Name);

        var profile = State.ToProfile();
        await _sshManager.StoreSecureShellProfile(profile);
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

            var profile = await _sshManager.GetSecureShellProfile(name);
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
            var profiles = await _sshManager.GetSecureShellProfilesSecure();
            RenderTable(profiles);

            var editOption = new Option("Edit...");
            var addOption = new Option("Add...");
            var ppkOption = new Option("Private Keys...");
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

    private async Task RenderEditOptionList(SecureShellProfileSecure[] profiles)
    {
        const string returnOption = "[yellow]Return[/]";

        var name = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .AddChoices(profiles.Select(x => x.Name).Concat(new[] { returnOption })));

        if (name == returnOption) return;

        var profile = profiles.Single(x => x.Name == name);

        var state = SecureShellScreenState.FromProfile(profile);

        await ScreenManager.PushAsync<SecureShellEditScreen, SecureShellScreenState>(state);
    }

    private static void RenderTable(IEnumerable<SecureShellProfileSecure> profiles)
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