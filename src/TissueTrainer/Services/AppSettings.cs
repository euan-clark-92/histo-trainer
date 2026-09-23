using System.IO;
using System.Text.Json;

namespace TissueTrainer.Services;

public sealed class AppSettings
{
    public string ContentPath { get; set; } = "content";
    public int TrainingSessionSize { get; set; } = 15;
    public int WeakSessionSize { get; set; } = 5;
    public int AssessmentSessionSize { get; set; } = 20;
    public int AssessmentPassPercent { get; set; } = 80;

    public static AppSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "app.config.json");
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new AppSettings();
    }

    public static string ResolveContentPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
    }
}
