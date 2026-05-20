using System.Text.Json.Serialization;

namespace TcpProducer.Admin;

public sealed class SerialsFileResponse
{
	[JsonPropertyName("content")]
	public string Content { get; init; } = "";

	[JsonPropertyName("lineCount")]
	public int LineCount { get; init; }
}

public sealed class SerialsSaveRequest
{
	[JsonPropertyName("content")]
	public string Content { get; set; } = "";
}

public static class SerialsFileService
{
	const string FileName = "deviceserials.txt";

	static string SourcePath => Path.Combine(AdminOptions.ProjectRoot, FileName);
	static string PublishPath => Path.Combine(AdminOptions.PublishDir, FileName);

	public static async Task<SerialsFileResponse> ReadAsync(CancellationToken cancellationToken = default)
	{
		var path = SourcePath;
		if (!File.Exists(path))
			throw new FileNotFoundException($"Не найден {path}");

		var content = await File.ReadAllTextAsync(path, cancellationToken);
		var lines = ParseLines(content);

		return new SerialsFileResponse
		{
			Content = content,
			LineCount = lines.Count,
		};
	}

	public static async Task<ConfigSaveResult> SaveAsync(
		string content,
		bool restart,
		CancellationToken cancellationToken = default)
	{
		var lines = ParseLines(content);
		if (lines.Count == 0)
			throw new ArgumentException("Добавьте хотя бы один серийный номер");

		if (lines.Count > 50_000)
			throw new ArgumentException("Слишком много строк (максимум 50000)");

		var normalized = string.Join(Environment.NewLine, lines) + Environment.NewLine;
		await WriteFileAsync(SourcePath, normalized, cancellationToken);
		await WriteFileAsync(PublishPath, normalized, cancellationToken);

		if (!restart)
		{
			return new ConfigSaveResult
			{
				Ok = true,
				Message = $"Сохранено {lines.Count} серийных номеров. Перезапустите tcpproducer.",
			};
		}

		var result = await ServiceManager.RestartAsync(cancellationToken);
		var ok = result.ExitCode == 0;

		return new ConfigSaveResult
		{
			Ok = ok,
			Restarted = ok,
			Message = ok
				? $"Сохранено {lines.Count} серийных номеров, tcpproducer перезапущен."
				: $"Файл сохранён ({lines.Count} строк), перезапуск не удался: {result.Output}",
		};
	}

	static List<string> ParseLines(string content) =>
		content
			.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
			.Select(l => l.Trim())
			.Where(l => l.Length > 0 && !l.StartsWith('#'))
			.ToList();

	static async Task WriteFileAsync(string path, string content, CancellationToken cancellationToken)
	{
		var temp = path + ".tmp";
		await File.WriteAllTextAsync(temp, content, cancellationToken);
		File.Move(temp, path, overwrite: true);
	}
}
