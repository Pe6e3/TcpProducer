using System.Text.Json.Serialization;

namespace TcpProducer.Admin;

public sealed class ServiceStatusResponse
{
	[JsonPropertyName("service")]
	public string Service { get; init; } = AdminOptions.ServiceName;

	[JsonPropertyName("activeState")]
	public string ActiveState { get; init; } = "";

	[JsonPropertyName("subState")]
	public string SubState { get; init; } = "";

	[JsonPropertyName("isRunning")]
	public bool IsRunning { get; init; }

	[JsonPropertyName("mainPid")]
	public int MainPid { get; init; }

	[JsonPropertyName("activeSince")]
	public string ActiveSince { get; init; } = "";

	[JsonPropertyName("raw")]
	public string Raw { get; init; } = "";
}

public sealed class ActionResponse
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; }

	[JsonPropertyName("output")]
	public string Output { get; init; } = "";
}

public static class ServiceManager
{
	static readonly CommandRunner Runner = new();

	public static async Task<ServiceStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
	{
		var show = await Runner.RunAsync(
			"systemctl",
			$"show {AdminOptions.ServiceName} --property=ActiveState,SubState,MainPID,ActiveEnterTimestamp",
			cancellationToken: cancellationToken);

		var status = await Runner.RunAsync(
			"systemctl",
			$"status {AdminOptions.ServiceName} --no-pager",
			cancellationToken: cancellationToken);

		var props = ParseProperties(show.Output);
		var activeState = props.GetValueOrDefault("ActiveState", "unknown");
		var subState = props.GetValueOrDefault("SubState", "unknown");
		_ = int.TryParse(props.GetValueOrDefault("MainPID", "0"), out var mainPid);

		return new ServiceStatusResponse
		{
			ActiveState = activeState,
			SubState = subState,
			IsRunning = activeState == "active" && subState == "running",
			MainPid = mainPid,
			ActiveSince = props.GetValueOrDefault("ActiveEnterTimestamp", ""),
			Raw = status.Output,
		};
	}

	public static Task<CommandResult> StartAsync(CancellationToken cancellationToken = default) =>
		Runner.RunAsync("systemctl", $"start {AdminOptions.ServiceName}", cancellationToken: cancellationToken);

	public static Task<CommandResult> StopAsync(CancellationToken cancellationToken = default) =>
		Runner.RunAsync("systemctl", $"stop {AdminOptions.ServiceName}", cancellationToken: cancellationToken);

	public static Task<CommandResult> RestartAsync(CancellationToken cancellationToken = default) =>
		Runner.RunAsync("systemctl", $"restart {AdminOptions.ServiceName}", cancellationToken: cancellationToken);

	public static Task<CommandResult> DeployAsync(CancellationToken cancellationToken = default) =>
		Runner.DeployAsync(cancellationToken);

	static Dictionary<string, string> ParseProperties(string output)
	{
		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			var idx = line.IndexOf('=');
			if (idx <= 0)
				continue;

			result[line[..idx]] = line[(idx + 1)..];
		}

		return result;
	}
}
