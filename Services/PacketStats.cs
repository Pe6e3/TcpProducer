using System.Text.Json;

namespace TcpClientDevice.Services;

public static class PacketStats
{
	static long _totalSent;
	static readonly string StatsPath = Path.Combine(AppContext.BaseDirectory, "stats.json");

	public static long TotalSent => Interlocked.Read(ref _totalSent);

	public static void RecordSent() => Interlocked.Increment(ref _totalSent);

	public static void StartPersistence(CancellationToken cancellationToken)
	{
		_ = Task.Run(async () =>
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					await PersistAsync(cancellationToken);
					await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}, CancellationToken.None);
	}

	static async Task PersistAsync(CancellationToken cancellationToken)
	{
		var snapshot = new PacketStatsSnapshot(TotalSent, DateTime.UtcNow);
		var json = JsonSerializer.Serialize(snapshot);
		var tempPath = StatsPath + ".tmp";

		await File.WriteAllTextAsync(tempPath, json, cancellationToken);
		File.Move(tempPath, StatsPath, overwrite: true);
	}

	public sealed record PacketStatsSnapshot(long TotalSent, DateTime UpdatedAt);
}
