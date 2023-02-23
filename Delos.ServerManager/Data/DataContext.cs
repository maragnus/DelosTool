using Delos.SecureShells;
using JetBrains.Annotations;
using LiteDB.Async;
using Microsoft.Extensions.Options;

namespace Delos.ServerManager.Data;

public class DataContextOptions
{
    public const string SectionName = "Data";
    public string ConnectionString { get; set; } = null!;
}

[PublicAPI]
public class DataContext : IDisposable
{
    private readonly LiteDatabaseAsync _database;

    public DataContext(IOptions<DataContextOptions> options)
    {
        _database = new LiteDatabaseAsync(options.Value.ConnectionString);
        SecureShellProfiles = _database.GetCollection<SecureShellSecureProfile>();
        PrivateKeys = _database.GetCollection<PrivateKeyProfile>("KeyPairs");
    }

    public ILiteCollectionAsync<SecureShellSecureProfile> SecureShellProfiles { get; }
    public ILiteCollectionAsync<PrivateKeyProfile> PrivateKeys { get; }

    public void Dispose()
    {
        _database.Dispose();
    }
}
