namespace TissueTrainer.Data.Database;

public static class DatabasePaths
{
    public static string GetDefaultDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "TissueTrainer", "progress.db");
    }

    public static string BuildConnectionString(string databasePath)
    {
        return new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
            Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Shared
        }.ToString();
    }
}
