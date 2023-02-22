using System.Security.Cryptography;
using Delos.ServerManager.Data;
using JetBrains.Annotations;
using LiteDB;

namespace Delos.ServerManager.SecureShells;

[PublicAPI]
public class SecureShellManager
{
    private readonly DataContext _dataContext;

    public SecureShellManager(DataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<PrivateKeyProfile?> GetPrivateKey(string name)
    {
        return await _dataContext.KeyPairs.Query()
            .Where(x => name == x.Name)
            .FirstOrDefaultAsync();
    }

    public async Task<PrivateKeyProfile[]> GetPrivateKeys()
    {
        var keys = await _dataContext.KeyPairs.Query()
            .ToListAsync();
        return keys.ToArray();
    }
    
    public async Task<PrivateKeyProfile[]> GetPrivateKeys(params string[] names)
    {
        var keys = await _dataContext.KeyPairs.Query()
            .Where(x => names.Contains(x.Name))
            .ToListAsync();
        return keys.ToArray();
    }

    public async Task ImportPrivateKeyPem(string name, string path)
    {
        using var rsa = RSA.Create();
        var keyFile = await File.ReadAllTextAsync(path);
        rsa.ImportFromPem(keyFile);
        await _dataContext.KeyPairs.InsertAsync(new PrivateKeyProfile(
            ObjectId.NewObjectId(), name, "RSA", rsa.ExportRSAPrivateKeyPem()));
    }

    public async Task StorePrivateKey(string name)
    {
        var key = GenerateRsaKey(name);

        var existingKey = await GetPrivateKey(name);
        if (existingKey != null)
            await _dataContext.KeyPairs.InsertAsync(key with { Id = existingKey.Id });
        else
            await _dataContext.KeyPairs.InsertAsync(key);
    }

    public static PrivateKeyProfile GenerateRsaKey(string name)
    {
        using var rsa = RSA.Create(4096);
        return new PrivateKeyProfile(ObjectId.NewObjectId(), name, "RSA", rsa.ExportRSAPrivateKeyPem());
    }

    /// <summary>
    /// Updates profile's name of <paramref name="oldName"/> with <paramref name="newName"/>, replacing profile if it already exists
    /// </summary>
    /// <param name="oldName">Current name of Profile</param>
    /// <param name="newName">New name for Profile</param>
    public async Task RenameSecureShellProfile(string oldName, string newName)
    {
        var profile = await GetSecureShellProfile(oldName);
        if (profile == null) return;
        var newProfile = await GetSecureShellProfile(newName);
        if (newProfile != null)
            await _dataContext.SecureShellProfiles.DeleteAsync(newProfile.Id);
        await _dataContext.SecureShellProfiles.UpdateAsync(profile.Id, profile with { Name = newName });
    }

    public async Task<SecureShell> CreateSecureShell(string name)
    {
        var profile = await GetSecureShellProfile(name);
        return new SecureShell(profile!, this); 
    }
    
    public async Task StoreSecureShellProfile(SecureShellProfileSecure profile)
    {
        var currentProfile = await GetSecureShellProfile(profile.Name);
        var id = currentProfile?.Id ?? ObjectId.NewObjectId();
        await _dataContext.SecureShellProfiles.UpsertAsync(id, profile with { Id = id });
    }

    public async Task<SecureShellProfileSecure?> GetSecureShellProfile(string name)
    {
        return await _dataContext.SecureShellProfiles.Query()
            .Where(x => x.Name == name)
            .FirstOrDefaultAsync();
    }

    public async Task<SecureShellProfile[]> GetSecureShellProfiles()
    {
        var profiles = await _dataContext.SecureShellProfiles.Query().ToListAsync();
        return profiles.Select(x => (SecureShellProfile)x).ToArray();
    }

    public async Task<SecureShellProfileSecure[]> GetSecureShellProfilesSecure()
    {
        var profiles = await _dataContext.SecureShellProfiles.Query().ToListAsync();
        return profiles.ToArray();
    }

    public async Task DeleteProfile(string name)
    {
        var profile = await GetSecureShellProfile(name);
        if (profile == null) return;
        await _dataContext.SecureShellProfiles.DeleteAsync(profile.Id);
    }

    public async Task DeletePrivateKey(string name)
    {
        var key = await GetPrivateKey(name);
        if (key == null) return;
        await _dataContext.KeyPairs.DeleteAsync(key.Id);
    }
}