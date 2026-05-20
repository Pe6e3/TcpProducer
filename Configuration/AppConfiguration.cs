using DotNetEnv;
using Microsoft.Extensions.Configuration;

namespace TcpClientDevice.Configuration;

public static class AppConfiguration
{
	public static AppOptions Load(string contentRoot)
	{
		LoadEnvFile(contentRoot);

		var configuration = new ConfigurationBuilder()
			.SetBasePath(contentRoot)
			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
			.AddEnvironmentVariables()
			.Build();

		var options = new AppOptions();
		configuration.Bind(options);
		Validate(options);
		return options;
	}

	static void LoadEnvFile(string contentRoot)
	{
		foreach (var dir in new[] { Directory.GetCurrentDirectory(), contentRoot })
		{
			var path = Path.Combine(dir, ".env");
			if (!File.Exists(path))
				continue;

			Env.Load(path);
			return;
		}
	}

	static void Validate(AppOptions options)
	{
		if (string.IsNullOrWhiteSpace(options.Tcp.Host))
			throw new InvalidOperationException("Задайте Tcp__Host в файле .env или переменных окружения");

		if (options.Tcp.Port is < 1 or > 65535)
			throw new InvalidOperationException("Задайте корректный Tcp__Port в .env (1–65535)");
	}
}
