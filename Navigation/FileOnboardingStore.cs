using System.IO;
using System.Text.Json;

namespace EcoQuest.Navigation;

/// <summary>
/// Keeps the ids of users who finished onboarding in one small JSON file (<c>%AppData%\EcoQuest\onboarding.json</c>).
/// Writes go to a temporary file first and replace the real one, so a crash never leaves half a file.
/// A missing, corrupt or unwritable file is never an error: it simply means "not completed".
/// </summary>
public sealed class FileOnboardingStore(string filePath) : IOnboardingStore
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    private readonly object _gate = new();

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EcoQuest", "onboarding.json");

    public bool HasCompleted(Guid userId)
    {
        lock (_gate)
        {
            return Read().Contains(userId);
        }
    }

    public void MarkCompleted(Guid userId)
    {
        lock (_gate)
        {
            var completed = Read();
            if (completed.Add(userId))
            {
                Write(completed);
            }
        }
    }

    private HashSet<Guid> Read()
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return [];
            }

            var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(filePath), Json);
            return document?.Completed is { } ids ? [.. ids] : [];
        }
        catch (Exception)
        {
            // Corrupt or unreadable file: treated as "nobody completed yet" (best effort by contract).
            return [];
        }
    }

    private void Write(HashSet<Guid> completed)
    {
        var temp = filePath + ".tmp";
        try
        {
            if (Path.GetDirectoryName(filePath) is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(temp, JsonSerializer.Serialize(new Document { Completed = [.. completed] }, Json));
            File.Move(temp, filePath, overwrite: true);
        }
        catch (Exception)
        {
            // Best effort: failing to remember must never block the user. The screen may show again next time.
            try
            {
                File.Delete(temp);
            }
            catch (Exception)
            {
                // Nothing more to do.
            }
        }
    }

    private sealed class Document
    {
        public List<Guid> Completed { get; set; } = [];
    }
}
