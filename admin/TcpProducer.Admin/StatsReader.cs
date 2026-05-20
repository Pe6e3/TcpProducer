using System.Text.Json;
using System.Text.Json.Serialization;

namespace TcpProducer.Admin;

public sealed class MinuteBucketPoint
{
	[JsonPropertyName("minute")]
	public int Minute { get; init; }

	[JsonPropertyName("count")]
	public long Count { get; init; }

	[JsonPropertyName("label")]
	public string Label { get; init; } = "";
}

public sealed class PacketStatsResponse
{
	[JsonPropertyName("totalSent")]
	public long TotalSent { get; init; }

	[JsonPropertyName("last60Minutes")]
	public long Last60Minutes { get; init; }

	[JsonPropertyName("updatedAt")]
	public DateTime? UpdatedAt { get; init; }

	[JsonPropertyName("available")]
	public bool Available { get; init; }

	[JsonPropertyName("timeline")]
	public List<MinuteBucketPoint> Timeline { get; init; } = [];
}

public static class StatsReader
{
	const int TimelineMinutes = 60;

	static readonly string StatsPath = Path.Combine(AdminOptions.PublishDir, "stats.json");

	static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	public static async Task<PacketStatsResponse> ReadAsync(CancellationToken cancellationToken = default)
	{
		if (!File.Exists(StatsPath))
		{
			return new PacketStatsResponse
			{
				TotalSent = 0,
				Last60Minutes = 0,
				Available = false,
			};
		}

		try
		{
			await using var stream = File.OpenRead(StatsPath);
			var snapshot = await JsonSerializer.DeserializeAsync<StatsFileDto>(stream, JsonOptions, cancellationToken);

			return new PacketStatsResponse
			{
				TotalSent = snapshot?.TotalSent ?? 0,
				Last60Minutes = snapshot?.Last60Minutes ?? 0,
				UpdatedAt = snapshot?.UpdatedAt,
				Available = true,
				Timeline = BuildTimeline(snapshot?.Buckets),
			};
		}
		catch
		{
			return new PacketStatsResponse
			{
				TotalSent = 0,
				Last60Minutes = 0,
				Available = false,
			};
		}
	}

	static List<MinuteBucketPoint> BuildTimeline(List<StatsBucketDto>? buckets)
	{
		var nowMinute = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60);
		var counts = new Dictionary<int, long>();

		if (buckets is not null)
		{
			foreach (var bucket in buckets)
				counts[bucket.Minute] = bucket.Count;
		}

		var timeline = new List<MinuteBucketPoint>(TimelineMinutes);

		for (var offset = TimelineMinutes - 1; offset >= 0; offset--)
		{
			var minute = nowMinute - offset;
			var count = counts.GetValueOrDefault(minute, 0L);
			var time = DateTimeOffset.FromUnixTimeSeconds(minute * 60L).ToLocalTime();

			timeline.Add(new MinuteBucketPoint
			{
				Minute = minute,
				Count = count,
				Label = time.ToString("HH:mm"),
			});
		}

		return timeline;
	}

	sealed class StatsFileDto
	{
		[JsonPropertyName("totalSent")]
		public long TotalSent { get; set; }

		[JsonPropertyName("last60Minutes")]
		public long Last60Minutes { get; set; }

		[JsonPropertyName("updatedAt")]
		public DateTime UpdatedAt { get; set; }

		[JsonPropertyName("buckets")]
		public List<StatsBucketDto>? Buckets { get; set; }
	}

	sealed class StatsBucketDto
	{
		[JsonPropertyName("minute")]
		public int Minute { get; set; }

		[JsonPropertyName("count")]
		public long Count { get; set; }
	}
}
