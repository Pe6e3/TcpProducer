using System.Runtime.CompilerServices;
using System.Text.Json;

namespace TcpProducer.Admin;

public static class StatsStreamer
{
	static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

	public static async IAsyncEnumerable<string> StreamAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			var stats = await StatsReader.ReadAsync(cancellationToken);
			var json = JsonSerializer.Serialize(stats);
			yield return $"data: {json}\n\n";
			await Task.Delay(Interval, cancellationToken);
		}
	}
}
