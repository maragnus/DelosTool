using Delos.ServerManager.SecureShells;
using LiteDB.Async;
using Microsoft.Extensions.Options;

namespace Delos.ServerManager.Data;

public class DataContextOptions
{
    public const string SectionName = "Data";
    public string ConnectionString { get; set; } = null!;
}

public class DataContext : IDisposable
{
    private readonly LiteDatabaseAsync _database;

    public DataContext(IOptions<DataContextOptions> options)
    {
        _database = new LiteDatabaseAsync(options.Value.ConnectionString);
        SecureShellProfiles = _database.GetCollection<SecureShellProfileSecure>();
        KeyPairs = _database.GetCollection<PrivateKeyProfile>();
    }

    public ILiteCollectionAsync<SecureShellProfileSecure> SecureShellProfiles { get; }
    public ILiteCollectionAsync<PrivateKeyProfile> KeyPairs { get; set; }

    public void Dispose()
    {
        _database.Dispose();
    }
}