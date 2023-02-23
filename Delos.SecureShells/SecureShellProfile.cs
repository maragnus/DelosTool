using System.Text.RegularExpressions;

namespace Delos.SecureShells;

public record SecureShellProfile(
    string? Id,
    string Name,
    string? Host,
    string? UserName)
{
    public string? MachineInfo { get; set; }
    
    public static bool IsNameValid(string name) =>
        Regex.IsMatch(name, @"^[A-Za-z]\w+$");

    public static bool IsHostValid(string value)
        => Uri.CheckHostName(value) != UriHostNameType.Unknown;
}

public record SecureShellSecureProfile(
    string? Id,
    string Name,
    string? Host,
    string? UserName,
    string? Password,
    string? RootPassword,
    string[]? KeyPairNames,
    byte[]? Fingerprint) : SecureShellProfile(Id, Name, Host, UserName)
{
    public SecureShellSecureProfile() : this(null, "", null, null, null, null, null, null)
    {
    }
}