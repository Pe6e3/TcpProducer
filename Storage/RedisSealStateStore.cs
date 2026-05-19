using System.Text.Json;
using StackExchange.Redis;
using TcpClientDevice.Seals;

namespace TcpClientDevice.Storage;

public sealed class RedisSealStateStore : ISealStateStore, IAsyncDisposable
{
	readonly ConnectionMultiplexer _redis;
	readonly IDatabase _db;

	public RedisSealStateStore(string connectionString)
	{
		_redis = ConnectionMultiplexer.Connect(connectionString);
		_db = _redis.GetDatabase();
	}

	static string StateKey(string sn) => $"seal:{sn}:state";
	static string QueueKey(string sn) => $"seal:{sn}:queue";
	static string AckKey(string sn) => $"seal:{sn}:ack";

	public async Task<SealState> GetStateAsync(string serialNumber, CancellationToken cancellationToken = default)
	{
		var value = await _db.StringGetAsync(StateKey(serialNumber));
		if (value.IsNullOrEmpty)
			return new SealState { SerialNumber = serialNumber };

		return JsonSerializer.Deserialize<SealState>(value.ToString())
			?? new SealState { SerialNumber = serialNumber };
	}

	public async Task SetStatusAsync(string serialNumber, SealStatus status, CancellationToken cancellationToken = default)
	{
		var state = await GetStateAsync(serialNumber, cancellationToken);
		state.Status = status;
		state.OpenedAtUtc = status == SealStatus.Open ? DateTime.UtcNow : null;
		await SaveStateAsync(state);
	}

	public async Task SetOnlineAsync(string serialNumber, bool isOnline, CancellationToken cancellationToken = default)
	{
		var state = await GetStateAsync(serialNumber, cancellationToken);
		state.IsOnline = isOnline;
		await SaveStateAsync(state);
	}

	public async Task<byte> GetNextAckSerialAsync(string serialNumber, CancellationToken cancellationToken = default)
	{
		var key = AckKey(serialNumber);
		if (!await _db.KeyExistsAsync(key))
			await _db.StringSetAsync(key, 255);

		var value = await _db.StringIncrementAsync(key);
		return (byte)(value % 256);
	}

	public async Task EnqueueTelemetryAsync(string serialNumber, byte[] packet, CancellationToken cancellationToken = default)
	{
		await _db.ListRightPushAsync(QueueKey(serialNumber), Convert.ToBase64String(packet));
	}

	public async Task<byte[]?> TryDequeueTelemetryAsync(string serialNumber, CancellationToken cancellationToken = default)
	{
		var value = await _db.ListLeftPopAsync(QueueKey(serialNumber));
		if (value.IsNullOrEmpty)
			return null;
		return Convert.FromBase64String(value.ToString());
	}

	public async Task<int> GetQueueLengthAsync(string serialNumber, CancellationToken cancellationToken = default)
	{
		var len = await _db.ListLengthAsync(QueueKey(serialNumber));
		return (int)len;
	}

	async Task SaveStateAsync(SealState state)
	{
		var json = JsonSerializer.Serialize(state);
		await _db.StringSetAsync(StateKey(state.SerialNumber), json);
	}

	public async ValueTask DisposeAsync()
	{
		await _redis.CloseAsync();
		_redis.Dispose();
	}
}
