namespace TcpProducer.Admin;

public static class AdminOptions
{
	public const string ServiceName = "tcpproducer";
	public const string ProjectRoot = "/var/www/TcpProducer";
	public const string PublishDir = "/var/www/TcpProducer/publish";

	public static string ApiToken { get; private set; } = "";

	public static void Load()
	{
		foreach (var dir in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory, AdminOptions.ProjectRoot })
		{
			var path = Path.Combine(dir, ".env");
			if (!File.Exists(path))
				continue;

			DotNetEnv.Env.Load(path);
			break;
		}

		ApiToken = Environment.GetEnvironmentVariable("ADMIN_API_TOKEN") ?? "";
		if (string.IsNullOrWhiteSpace(ApiToken))
			throw new InvalidOperationException("Задайте ADMIN_API_TOKEN в .env");
	}
}
