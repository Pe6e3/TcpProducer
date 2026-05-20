using System.Text.Json;
using System.Text.Json.Serialization;

namespace TcpProducer.Admin;

public sealed class PacketStatsResponse
{
	[JsonPropertyName("totalSent")]
	public long TotalSent { get; init; }

	[JsonPropertyName("updatedAt")]
	public DateTime? UpdatedAt { get; init; }

	[JsonPropertyName("available")]
	public bool Available { get; init; }
}

public static class StatsReader
{
	static readonly string StatsPath = Path.Combine(AdminOptions.PublishDir, "stats.json");

	public static async Task<PacketStatsResponse> ReadAsync(CancellationToken cancellationToken = default)
	{
		if (!File.Exists(StatsPath))
		{
			return new PacketStatsResponse
			{
				TotalSent = 0,
				Available = false,
			};
		}

		try
		{
			await using var stream = File.OpenRead(StatsPath);
			var snapshot = await JsonSerializer.DeserializeAsync<StatsFileDto>(stream, cancellationToken: cancellationToken);

			return new PacketStatsResponse
			{
				TotalSent = snapshot?.TotalSent ?? 0,
				UpdatedAt = snapshot?.UpdatedAt,
				Available = true,
			};
		}
		catch
		{
			return new PacketStatsResponse
			{
				TotalSent = 0,
				Available = false,
			};
		}
	}

	sealed class StatsFileDto
	{
		[JsonPropertyName("TotalSent")]
		public long TotalSent { get; set; }

		[JsonPropertyName("UpdatedAt")]
		public DateTime UpdatedAt { get; set; }
	}
}
