namespace TcpClientDevice.Configuration;

public static class SealSerialNumbersLoader
{
	public static IReadOnlyList<string> Load(string path)
	{
		if (!File.Exists(path))
			throw new FileNotFoundException($"Файл серийных номеров не найден: {path}");

		return File.ReadAllLines(path)
			.Select(l => l.Trim())
			.Where(l => l.Length > 0 && !l.StartsWith('#'))
			.ToList();
	}

	public static string ResolvePath(string fileName, string basePath) =>
		Path.IsPathRooted(fileName) ? fileName : Path.Combine(basePath, fileName);
}
