namespace EcoQuest.Infrastructure;

public interface IAppPaths
{
    string DataRoot { get; }
    string PhotosRoot { get; }
    string AvatarsRoot { get; }
    string LocationKeysFile { get; }
}
