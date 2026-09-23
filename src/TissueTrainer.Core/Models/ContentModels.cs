namespace TissueTrainer.Core.Models;

public sealed class ContentManifest
{
    public string Version { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PublishedDate { get; set; } = string.Empty;
    public string MinAppVersion { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public string Author { get; set; } = string.Empty;
}

public sealed class CategoryDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Parent { get; set; }
    public string OrganSystem { get; set; } = string.Empty;
    public string Stain { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public double CurriculumWeight { get; set; } = 1.0;
}

public sealed class QuestionDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "mcq";
    public string Image { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public IReadOnlyList<string> Options { get; set; } = Array.Empty<string>();
    public int CorrectIndex { get; set; }
    public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();
    public string Explanation { get; set; } = string.Empty;
    public IReadOnlyList<string> Features { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ConfusableWith { get; set; } = Array.Empty<string>();
    public string Difficulty { get; set; } = "beginner";
    public bool Active { get; set; } = true;
}

public sealed class ConfusionPair
{
    public string Id { get; set; } = string.Empty;
    public string CategoryA { get; set; } = string.Empty;
    public string CategoryB { get; set; } = string.Empty;
    public string TrainerNote { get; set; } = string.Empty;
}

public sealed class ContentPack
{
    public ContentManifest Manifest { get; set; } = new();
    public IReadOnlyList<CategoryDefinition> Categories { get; set; } = Array.Empty<CategoryDefinition>();
    public IReadOnlyList<QuestionDefinition> Questions { get; set; } = Array.Empty<QuestionDefinition>();
    public IReadOnlyList<ConfusionPair> ConfusionPairs { get; set; } = Array.Empty<ConfusionPair>();
    public string RootPath { get; set; } = string.Empty;
}
