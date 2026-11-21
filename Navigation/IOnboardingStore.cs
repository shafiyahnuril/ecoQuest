namespace EcoQuest.Navigation;

/// <summary>
/// Remembers per user whether the onboarding (UIR-72) was finished or skipped. Best effort: implementations never throw and
/// never block the app; when the state cannot be read, the user counts as "not completed".
/// </summary>
public interface IOnboardingStore
{
    bool HasCompleted(Guid userId);

    void MarkCompleted(Guid userId);
}
