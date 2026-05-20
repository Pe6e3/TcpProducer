using System.Text.Json;
using System.Text.Json.Serialization;

namespace TcpClientDevice.Services;

public static class PacketStats
{
	const int BucketCount = 60;

	static long _totalSent;
	static readonly long[] _counts = new long[BucketCount];
	static readonly int[] _minuteKeys = new int[BucketCount];
	static int _currentMinute = -1;
	static readonly object Gate = new();
	static readonly SemaphoreSlim PersistGate = new(1, 1);
	static readonly string StatsPath = Path.Combine(AppContext.BaseDirectory, "stats.json");

	public static long TotalSent => Interlocked.Read(ref _totalSent);

	public static void Initialize()
	{
		TryLoadFromFile();
	}

	/// <summary>
	/// Пишет актуальные счётчики и корзины в stats.json раз в секунду для панели (SSE).
	/// </summary>
	public static void StartLivePersistence(CancellationToken cancellationToken)
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

	public static void RecordSent()
	{
		Interlocked.Increment(ref _totalSent);

		var minute = CurrentUnixMinute();

		lock (Gate)
		{
			if (_currentMinute != minute)
				_currentMinute = minute;

			var idx = minute % BucketCount;
			if (_minuteKeys[idx] != minute)
			{
				_minuteKeys[idx] = minute;
				_counts[idx] = 0;
			}

			_counts[idx]++;
		}
	}

	public static PacketStatsSnapshot CreateSnapshot()
	{
		lock (Gate)
		{
			return new PacketStatsSnapshot(
				Interlocked.Read(ref _totalSent),
				SumLast60MinutesLocked(),
				DateTime.UtcNow,
				ExportBucketsLocked());
		}
	}

	static long SumLast60MinutesLocked()
	{
		var nowMinute = CurrentUnixMinute();
		var minMinute = nowMinute - (BucketCount - 1);
		long sum = 0;

		for (var i = 0; i < BucketCount; i++)
		{
			var key = _minuteKeys[i];
			if (key >= minMinute && key <= nowMinute)
				sum += _counts[i];
		}

		return sum;
	}

	static List<MinuteBucketDto> ExportBucketsLocked()
	{
		var list = new List<MinuteBucketDto>(BucketCount);

		for (var i = 0; i < BucketCount; i++)
		{
			if (_minuteKeys[i] <= 0)
				continue;

			list.Add(new MinuteBucketDto(_minuteKeys[i], _counts[i]));
		}

		return list;
	}

	static void TryLoadFromFile()
	{
		if (!File.Exists(StatsPath))
			return;

		try
		{
			var json = File.ReadAllText(StatsPath);
			var snapshot = JsonSerializer.Deserialize<PacketStatsSnapshot>(json);
			if (snapshot is null)
				return;

			lock (Gate)
			{
				Interlocked.Exchange(ref _totalSent, snapshot.TotalSent);
				Array.Clear(_counts, 0, BucketCount);
				Array.Clear(_minuteKeys, 0, BucketCount);
				_currentMinute = CurrentUnixMinute();

				foreach (var bucket in snapshot.Buckets)
				{
					if (bucket.Count <= 0)
						continue;

					var idx = bucket.Minute % BucketCount;
					_minuteKeys[idx] = bucket.Minute;
					_counts[idx] = bucket.Count;
				}
			}
		}
		catch
		{
			// старт с пустой статистикой
		}
	}

	static async Task PersistAsync(CancellationToken cancellationToken = default)
	{
		await PersistGate.WaitAsync(cancellationToken);
		try
		{
			var snapshot = CreateSnapshot();
			var json = JsonSerializer.Serialize(snapshot);
			var tempPath = StatsPath + ".tmp";

			await File.WriteAllTextAsync(tempPath, json, cancellationToken);
			File.Move(tempPath, StatsPath, overwrite: true);
		}
		catch
		{
			// не блокируем отправку пакетов
		}
		finally
		{
			PersistGate.Release();
		}
	}

	static int CurrentUnixMinute() =>
		(int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60);

	public sealed record PacketStatsSnapshot(
		[property: JsonPropertyName("totalSent")] long TotalSent,
		[property: JsonPropertyName("last60Minutes")] long Last60Minutes,
		[property: JsonPropertyName("updatedAt")] DateTime UpdatedAt,
		[property: JsonPropertyName("buckets")] List<MinuteBucketDto> Buckets);

	public sealed record MinuteBucketDto(
		[property: JsonPropertyName("minute")] int Minute,
		[property: JsonPropertyName("count")] long Count);
}
