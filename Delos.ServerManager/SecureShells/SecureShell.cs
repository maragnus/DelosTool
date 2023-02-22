using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Renci.SshNet;

namespace Delos.ServerManager.SecureShells;

[PublicAPI]
public class SecureShell
{
    public SecureShellProfile Profile => _profile with { };
    private readonly SecureShellManager _secureShellManager;
    private readonly SecureShellProfileSecure _profile;

    public SecureShell(SecureShellProfileSecure profile, SecureShellManager secureShellManager)
    {
        _profile = profile;
        _secureShellManager = secureShellManager;
    }

    public async Task<SshClientAsync> ConnectAsync()
    {
        var methods = new List<AuthenticationMethod>();
        
        if (_profile.Password != null)
            methods.Add(
                new PasswordAuthenticationMethod(_profile.UserName, _profile.Password));

        if (_profile.KeyPairNames?.Length > 0)
        {
            var keys = await _secureShellManager.GetPrivateKeys(_profile.KeyPairNames);
            methods.Add(
                new PrivateKeyAuthenticationMethod(_profile.UserName,
                    keys.Select(x => x.ToPrivateKeyFile()).ToArray()));
        }
        
        var connectionInfo = new ConnectionInfo(_profile.Host, _profile.UserName, methods.ToArray());

        var client = new SshClientAsync(connectionInfo);
        await client.ConnectAsync();
        return client;
    }

    public async Task InstallKeyPair(params string[] keyPairNames)
    {
        var keys = await _secureShellManager.GetPrivateKeys(keyPairNames);
        var client = await ConnectAsync();
        try
        {
            var result = await client.SendCommandAsync("cat ~/.ssh/authorized_keys");
            var registeredKeys =
                Regex.Matches(result.Result, @"^\s*ssh-rsa\s+([A-Za-z0-9+/]+=*)",
                        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase)
                    .Select(x => x.Groups[1].Value)
                    .ToHashSet();

            foreach (var key in keys)
            {
                var base64 = key.ToRfcPublicKey();
                if (registeredKeys.Contains(base64))
                    continue;
                var appendResult =
                    await client.SendCommandAsync($@"echo ""ssh-rsa {base64} {key.Name}"" >> ~/.ssh/authorized_keys");
                if (appendResult.ExitCode != 0)
                    throw new Exception($"Failed to append key {key.Name}: {appendResult.Error}");
            }
        }
        finally
        {
            await client.DisconnectAsync();
        }
    }
}