using System.Security.Cryptography;
using Delos.SecureShells;
using LiteDB;

namespace Delos.ServerManager.Data;

public class PrivateKeyStore : IPrivateKeyStore
{
    private readonly DataContext _dataContext;

    public PrivateKeyStore(DataContext dataContext)
    {
        _dataContext = dataContext;
    }

    static PrivateKeyProfile GenerateRsaKey(string name)
    {
        using var rsa = RSA.Create(4096);
        return new PrivateKeyProfile(ObjectId.NewObjectId().ToString(), name, "RSA", rsa.ExportRSAPrivateKeyPem());
    }
    
    public async Task Store(PrivateKeyProfile profile)
    {
        var currentProfile = await Get(profile.Name);
        var id = currentProfile?.Id ?? ObjectId.NewObjectId().ToString();
        await _dataContext.PrivateKeys.UpsertAsync(id, profile with { Id = id });
    }

    public async Task Rename(string oldName, string newName)
    {
        var profile = await Get(oldName);
        if (profile == null) return;
        var newProfile = await Get(newName);
        if (newProfile != null)
            await _dataContext.PrivateKeys.DeleteAsync(newProfile.Id);
        await _dataContext.PrivateKeys.UpdateAsync(profile.Id, profile with { Name = newName });
    }

    public async Task Delete(string name)
    {
        var profile = await Get(name);
        if (profile == null) return;
        await _dataContext.PrivateKeys.DeleteAsync(profile.Id);
    }

    public async Task<PrivateKeyProfile?> Get(string name)
    {
        return await _dataContext.PrivateKeys.Query()
            .Where(x => name == x.Name)
            .FirstOrDefaultAsync();
    }

    public async Task<PrivateKeyProfile[]> Get(params string[] names)
    {
        return await _dataContext.PrivateKeys.Query()
            .Where(x => names.Contains(x.Name))
            .ToArrayAsync();
    }

    public async Task<PrivateKeyProfile[]> Get()
    {
        var keys = await _dataContext.PrivateKeys.Query()
            .ToListAsync();
        return keys.ToArray();
    }

    public async Task StoreNew(string name)
    {
        var key = GenerateRsaKey(name);

        var existingKey = await Get(name);
        if (existingKey != null)
            await _dataContext.PrivateKeys.InsertAsync(key with { Id = existingKey.Id });
        else
            await _dataContext.PrivateKeys.InsertAsync(key);
    }

    public async Task Import(string name, string pemPath)
    {
        using var rsa = RSA.Create();
        var keyFile = await File.ReadAllTextAsync(pemPath);
        rsa.ImportFromPem(keyFile);
        await _dataContext.PrivateKeys.InsertAsync(new PrivateKeyProfile(
            ObjectId.NewObjectId().ToString(), name, "RSA", rsa.ExportRSAPrivateKeyPem()));
    }

    public async Task<string[]> GetSecureShellsUsing(string name)
    {
        var shells = await _dataContext.SecureShellProfiles.Query()
            .Where(x => x.KeyPairNames != null)
            .Select(x => new { Name = x.Name, Keys = x.KeyPairNames })
            .ToListAsync();

        return shells
            .Where(x => x.Keys!.Contains(name))
            .Select(x => x.Name)
            .ToArray();
    }
}