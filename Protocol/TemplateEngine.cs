namespace TcpClientDevice.Protocol;

public static class TemplateEngine
{
	public static string Apply(string template, string serialNumber, IReadOnlyList<string> parameters)
	{
		var result = template.Replace("{SerialNumber}", serialNumber, StringComparison.OrdinalIgnoreCase);

		for (var i = 0; i < parameters.Count; i++)
			result = result.Replace($"{{{i}}}", parameters[i], StringComparison.Ordinal);

		if (parameters.Count > 0)
		{
			result = result.Replace("{LastParameter}", parameters[^1], StringComparison.OrdinalIgnoreCase);
			result = result.Replace("{Last}", parameters[^1], StringComparison.OrdinalIgnoreCase);
		}

		for (var i = 0; i < parameters.Count; i++)
			result = result.Replace($"{{Param{i + 1}}}", parameters[i], StringComparison.OrdinalIgnoreCase);

		return result;
	}
}
