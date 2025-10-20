namespace TaskManagement.Infrastructure.Configuration;

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
    public string ProjectsCollection { get; set; } = "projects";
    public string TasksCollection { get; set; } = "tasks";
}

public class RedisSettings
{
    public string ConnectionString { get; set; } = null!;
    public int DefaultExpirationMinutes { get; set; } = 30;
}

public class SupabaseSettings
{
    public string Url { get; set; } = null!;
    public string AnonKey { get; set; } = null!;
    public string JwtSecret { get; set; } = null!;
}
