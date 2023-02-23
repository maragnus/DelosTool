using Delos.SecureShells;
using LiteDB;

namespace Delos.ServerManager.Data;

public class SecureShellStore : ISecureShellStore
{
    private readonly DataContext _dataContext;

    public SecureShellStore(DataContext dataContext)
    {
        _dataContext = dataContext;
    }
    
    public async Task Store(SecureShellSecureProfile profile)
    {
        var currentProfile = await Get(profile.Name);
        var id = currentProfile?.Id ?? ObjectId.NewObjectId().ToString();
        await _dataContext.SecureShellProfiles.UpsertAsync(id, profile with { Id = id });
    }

    public async Task Rename(string oldName, string newName)
    { 
        var profile = await Get(oldName);
        if (profile == null) return;
        var newProfile = await Get(newName);
        if (newProfile != null)
            await _dataContext.SecureShellProfiles.DeleteAsync(newProfile.Id);
        await _dataContext.SecureShellProfiles.UpdateAsync(profile.Id, profile with { Name = newName });
    }

    public async Task Delete(string name)
    {
        var profile = await Get(name);
        if (profile == null) return;
        await _dataContext.SecureShellProfiles.DeleteAsync(profile.Id);
    }

    public async Task<SecureShellSecureProfile?> Get(string name)
    {
        return await _dataContext.SecureShellProfiles.Query()
            .Where(x => x.Name == name)
            .FirstOrDefaultAsync();
    }

    public async Task<SecureShellSecureProfile[]> Get(params string[] names)
    {
        return await _dataContext.SecureShellProfiles.Query()
            .Where(x => names.Contains(x.Name))
            .ToArrayAsync();
    }

    public async Task<SecureShellSecureProfile[]> Get()
    {
        var profiles = await _dataContext.SecureShellProfiles.Query().ToListAsync();
        return profiles.ToArray();
    }
}