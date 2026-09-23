namespace TissueTrainer.Models;

public sealed class QuizOptionItem
{
    public int Index { get; init; }
    public string Text { get; init; } = string.Empty;
    public bool IsSelected { get; set; }
    public bool IsCorrectOption { get; set; }
    public bool IsRevealed { get; set; }
    public bool IsWrongSelection { get; set; }
}
