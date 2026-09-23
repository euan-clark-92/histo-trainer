using TissueTrainer.Core.Models;

namespace TissueTrainer.Core.Services;

public sealed class MasteryCalculator
{
    public MasteryStatus CalculateStatus(
        int attempts,
        double accuracy,
        double weakThreshold = 0.75,
        double masteredThreshold = 0.90,
        int minAttemptsForMastered = 10)
    {
        if (attempts >= 5 && accuracy < weakThreshold)
        {
            return MasteryStatus.Weak;
        }

        if (attempts >= minAttemptsForMastered && accuracy >= masteredThreshold)
        {
            return MasteryStatus.Mastered;
        }

        return MasteryStatus.Learning;
    }

    public CategoryProgress UpdateProgress(CategoryProgress progress, bool correct, DateTimeOffset now)
    {
        progress.Attempts += 1;
        if (correct)
        {
            progress.Correct += 1;
        }

        progress.LastSeenAt = now;
        progress.Status = CalculateStatus(progress.Attempts, progress.Accuracy);
        return progress;
    }
}
