if (args.Contains("--healthcheck"))
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var response = await client.GetAsync("http://localhost:8080/health");
        Environment.Exit(response.IsSuccessStatusCode ? 0 : 1);
    }
    catch
    {
        Environment.Exit(1);
    }
}

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

var environment =
    Environment.GetEnvironmentVariable("APP_ENVIRONMENT")
    ?? "development";

var version =
    Environment.GetEnvironmentVariable("APP_VERSION")
    ?? "local";

app.MapGet("/", () => new
{
    application = "DevOps Web API",
    version,
    environment
});

app.MapGet("/health", () => new
{
    status = "healthy",
    version,
    environment,
    timestamp = DateTime.UtcNow
});

app.MapGet("/version", () => new
{
    version,
    environment,
    message = "Nova funcionalidade da versão 1.1"
});

app.Run();