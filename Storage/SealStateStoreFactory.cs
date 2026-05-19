using TcpClientDevice.Configuration;

namespace TcpClientDevice.Storage;

public static class SealStateStoreFactory
{
	public static ISealStateStore Create(StorageOptions options) =>
		options.Provider.Equals("Redis", StringComparison.OrdinalIgnoreCase)
			? new RedisSealStateStore(options.RedisConnection)
			: new InMemorySealStateStore();
}
