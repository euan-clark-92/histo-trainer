using System.Text.Json;
using System.Text.Json.Serialization;
using TissueTrainer.Core.Models;

namespace TissueTrainer.Core.Services;

public sealed class ContentLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<ContentPack> LoadAsync(string contentRoot, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(contentRoot))
        {
            throw new DirectoryNotFoundException($"Content pack not found at '{contentRoot}'.");
        }

        var manifestPath = Path.Combine(contentRoot, "manifest.json");
        var categoriesPath = Path.Combine(contentRoot, "categories.json");
        var questionsPath = Path.Combine(contentRoot, "questions.json");
        var confusionPath = Path.Combine(contentRoot, "confusion_pairs.json");

        var manifest = await ReadRequiredAsync<ContentManifest>(manifestPath, cancellationToken);
        var categories = await ReadRequiredAsync<CategoriesFile>(categoriesPath, cancellationToken);
        var questions = await ReadRequiredAsync<QuestionsFile>(questionsPath, cancellationToken);
        var confusion = File.Exists(confusionPath)
            ? await ReadRequiredAsync<ConfusionPairsFile>(confusionPath, cancellationToken)
            : new ConfusionPairsFile();

        Validate(manifest, categories.Categories, questions.Questions, contentRoot);

        return new ContentPack
        {
            Manifest = manifest,
            Categories = categories.Categories,
            Questions = questions.Questions.Where(q => q.Active).ToList(),
            ConfusionPairs = confusion.Pairs,
            RootPath = contentRoot
        };
    }

    private static void Validate(
        ContentManifest manifest,
        IReadOnlyList<CategoryDefinition> categories,
        IReadOnlyList<QuestionDefinition> questions,
        string contentRoot)
    {
        var errors = new List<string>();
        var categoryIds = categories.Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var questionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var question in questions)
        {
            if (!questionIds.Add(question.Id))
            {
                errors.Add($"Duplicate question id '{question.Id}'.");
            }

            if (question.Options.Count < 2)
            {
                errors.Add($"Question '{question.Id}' must have at least 2 options.");
            }

            if (question.CorrectIndex < 0 || question.CorrectIndex >= question.Options.Count)
            {
                errors.Add($"Question '{question.Id}' has invalid correct_index.");
            }

            var imagePath = Path.Combine(contentRoot, question.Image.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(imagePath))
            {
                errors.Add($"Question '{question.Id}' references missing image '{question.Image}'.");
            }

            foreach (var categoryId in question.Categories)
            {
                if (!categoryIds.Contains(categoryId))
                {
                    errors.Add($"Question '{question.Id}' references unknown category '{categoryId}'.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Content pack validation failed:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors));
        }

        if (manifest.QuestionCount != questions.Count(q => q.Active))
        {
            // Non-fatal warning handled by caller if needed.
        }
    }

    private static async Task<T> ReadRequiredAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Required content file not found: {path}");
        }

        await using var stream = File.OpenRead(path);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException($"Failed to deserialize '{path}'.");
    }

    private sealed class CategoriesFile
    {
        public List<CategoryDefinition> Categories { get; set; } = new();
    }

    private sealed class QuestionsFile
    {
        public List<QuestionDefinition> Questions { get; set; } = new();
    }

    private sealed class ConfusionPairsFile
    {
        public List<ConfusionPair> Pairs { get; set; } = new();
    }
}
