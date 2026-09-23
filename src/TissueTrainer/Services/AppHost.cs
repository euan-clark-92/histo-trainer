using System.IO;
using TissueTrainer.Core.Models;
using TissueTrainer.Core.Services;
using TissueTrainer.Data.Database;
using TissueTrainer.Data.Repositories;

namespace TissueTrainer.Services;

public sealed class AppHost
{
    public AppSettings Settings { get; private set; } = new();
    public string ConnectionString { get; private set; } = string.Empty;
    public ContentPack ContentPack { get; private set; } = new();
    public long CurrentUserId { get; private set; }

    public ContentLoader ContentLoader { get; } = new();
    public AdaptiveQuestionSelector QuestionSelector { get; } = new();
    public ProgressRepository ProgressRepository { get; private set; } = null!;
    public SessionRepository SessionRepository { get; private set; } = null!;
    public UserRepository UserRepository { get; private set; } = null!;
    public TrainingSessionService TrainingSessionService { get; private set; } = null!;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Settings = AppSettings.Load();

        var dbPath = DatabasePaths.GetDefaultDatabasePath();
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        ConnectionString = DatabasePaths.BuildConnectionString(dbPath);

        var migrationsDir = Path.Combine(AppContext.BaseDirectory, "Migrations");
        var migrationRunner = new MigrationRunner(ConnectionString, migrationsDir);
        await migrationRunner.MigrateAsync(cancellationToken);

        ProgressRepository = new ProgressRepository(ConnectionString);
        SessionRepository = new SessionRepository(ConnectionString);
        UserRepository = new UserRepository(ConnectionString);
        TrainingSessionService = new TrainingSessionService(this);

        CurrentUserId = await UserRepository.GetOrCreateUserAsync("Training Associate", cancellationToken: cancellationToken);
        await ReloadContentAsync(cancellationToken);
    }

    public async Task ReloadContentAsync(CancellationToken cancellationToken = default)
    {
        var contentPath = AppSettings.ResolveContentPath(Settings.ContentPath);
        ContentPack = await ContentLoader.LoadAsync(contentPath, cancellationToken);
    }

    public string GetCategoryLabel(string categoryId)
    {
        return ContentPack.Categories.FirstOrDefault(c =>
            c.Id.Equals(categoryId, StringComparison.OrdinalIgnoreCase))?.Label ?? categoryId;
    }

    public string GetImagePath(QuestionDefinition question)
    {
        return Path.GetFullPath(Path.Combine(ContentPack.RootPath, question.Image.Replace('/', Path.DirectorySeparatorChar)));
    }
}
