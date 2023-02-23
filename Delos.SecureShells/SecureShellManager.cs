using JetBrains.Annotations;

namespace Delos.SecureShells;

public interface IProfileStore<TProfile> where TProfile : class, new()
{
    Task Store(TProfile profile);
    Task Rename(string oldName, string newName);
    Task Delete(string name);
    Task<TProfile?> Get(string name);
    Task<TProfile[]> Get(params string[] name);
    Task<TProfile[]> Get();
}

public interface ISecureShellStore : IProfileStore<SecureShellSecureProfile>
{

}

public interface IPrivateKeyStore : IProfileStore<PrivateKeyProfile>
{
    Task StoreNew(string name);
    Task Import(string name, string pemPath);
    
    Task<string[]> GetSecureShellsUsing(string name);
}

[PublicAPI]
public class SecureShellManager
{
    public ISecureShellStore SecureShellStore { get; }
    public IPrivateKeyStore PrivateKeyStore { get; }

    public SecureShellManager(ISecureShellStore sshStore, IPrivateKeyStore ppkStore)
    {
        SecureShellStore = sshStore;
        PrivateKeyStore = ppkStore;
    }

    public async Task<SecureShell> CreateSecureShell(string name)
    {
        var profile = await SecureShellStore.Get(name);
        return new SecureShell(profile!, this); 
    }
}