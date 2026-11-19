using System;
using System.IO;

namespace EcoQuest.Infrastructure;

/// <summary>
/// Menyediakan lokasi folder penyimpanan data lokal EcoQuest (foto, avatar, log, dan cache lokasi).
/// </summary>
public sealed class AppPaths : IAppPaths
{
    public AppPaths()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EcoQuest"))
    {
    }

    public AppPaths(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        DataRoot = dataRoot;
    }

    public string DataRoot { get; }
    public string PhotosRoot => Path.Combine(DataRoot, "photos");
    public string AvatarsRoot => Path.Combine(DataRoot, "avatars");
    public string LogsRoot => Path.Combine(DataRoot, "logs");
    public string LocationKeysFile => Path.Combine(DataRoot, "locationkeys.json");
}
