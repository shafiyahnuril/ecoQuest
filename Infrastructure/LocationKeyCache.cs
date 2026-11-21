using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace EcoQuest.Infrastructure;

public sealed class LocationKeyCache(IAppPaths paths)
{
    private readonly object _gate = new();
    private Dictionary<string, string>? _keys;

    public async Task<string?> GetOrResolveAsync(string city, Func<Task<string?>> resolve)
    {
        lock (_gate)
        {
            _keys ??= Load();
            if (_keys.TryGetValue(city, out var cached)) return cached;
        }

        var key = await resolve();
        if (key is null) return null;

        lock (_gate)
        {
            _keys![city] = key;
            Save(_keys);
        }
        return key;
    }

    private Dictionary<string, string> Load()
    {
        try
        {
            if (!File.Exists(paths.LocationKeysFile)) return new();
            var json = File.ReadAllText(paths.LocationKeysFile);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        catch
        {
            return new();   
        }
    }

    private void Save(Dictionary<string, string> keys)
    {
        try
        {
            var file = paths.LocationKeysFile;
            var tmp = file + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(tmp, JsonSerializer.Serialize(keys));
            File.Move(tmp, file, overwrite: true);   
        }
        catch
        {
            
        }
    }
}
