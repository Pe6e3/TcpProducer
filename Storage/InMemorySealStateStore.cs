using System.Collections.Concurrent;
using TcpClientDevice.Seals;

namespace TcpClientDevice.Storage;

public sealed class InMemorySealStateStore : ISealStateStore
{
	readonly ConcurrentDictionary<string, SealEntry> _entries = new(StringComparer.Ordinal);

	public Task<SealState> GetStateAsync(string serialNumber, CancellationToken cancellationToken = default)
	{
		var entry = GetOrCreate(serialNumber);
		lock (entry.Sync)
			return Task.FromResult(CloneState(entry));
	}

	public Task SetStatusAsync(string serialNumber, SealStatus status, CancellationToken cancellationToken = default)
	{
		var entry = GetOrCreate(serialNumber);
		lock (entry.Sync)
		{
			entry.State.Status = status;
			entry.State.OpenedAtUtc = status == SealStatus.Open ? DateTime.UtcNow : null;
		}

		return Task.CompletedTask;
	}

	public Task SetOnlineAsync(string serialNumber, bool isOnline, CancellationToken cancellationToken = default)
	{
		var entry = GetOrCreate(serialNumber);
		lock (entry.Sync)
			entry.State.IsOnline = isOnline;
		return Task.CompletedTask;
	}

	public Task<byte> GetNextAckSerialAsync(string serialNumber, CancellationToken cancellationToken = default)
	{
		var entry = GetOrCreate(serialNumber);
		lock (entry.Sync)
		{
			entry.AckSerial = (byte)((entry.AckSerial + 1) & 0xFF);
			return Task.FromResult(entry.AckSerial);
		}
	}

	public Task EnqueueTelemetryAsync(string serialNumber, byte[] packet, CancellationToken cancellationToken = default)
	{
		var entry = GetOrCreate(serialNumber);
		lock (entry.Sync)
			entry.Queue.Enqueue(packet.ToArray());
		return Task.CompletedTask;
	}

	public Task<byte[]?> TryPeekTelemetryAsync(string serialNumber, CancellationToken cancellationToken = default)
	{
		var entry = GetOrCreate(serialNumber);
		lock (entry.Sync)
		{
			if (entry.Queue.Count == 0)
				return Task.FromResult<byte[]?>(null);
			return Task.FromResult<byte[]?>(entry.Queue.Peek().ToArray());
		}
	}

	public Task<byte[]?> TryDequeueTelemetryAsync(string serialNumber, CancellationToken cancellationToken = default)
	{
		var entry = GetOrCreate(serialNumber);
		lock (entry.Sync)
		{
			if (entry.Queue.Count == 0)
				return Task.FromResult<byte[]?>(null);
			return Task.FromResult<byte[]?>(entry.Queue.Dequeue());
		}
	}

	public Task<int> GetQueueLengthAsync(string serialNumber, CancellationToken cancellationToken = default)
	{
		var entry = GetOrCreate(serialNumber);
		lock (entry.Sync)
			return Task.FromResult(entry.Queue.Count);
	}

	SealEntry GetOrCreate(string serialNumber)
	{
		return _entries.GetOrAdd(serialNumber, _ => new SealEntry
		{
			State = new SealState { SerialNumber = serialNumber },
		});
	}

	static SealState CloneState(SealEntry entry) => new()
	{
		SerialNumber = entry.State.SerialNumber,
		Status = entry.State.Status,
		IsOnline = entry.State.IsOnline,
		OpenedAtUtc = entry.State.OpenedAtUtc,
	};

	sealed class SealEntry
	{
		public SealState State { get; init; } = new();
		public Queue<byte[]> Queue { get; } = new();
		public byte AckSerial { get; set; } = 0xFF;
		public object Sync { get; } = new();
	}
}
