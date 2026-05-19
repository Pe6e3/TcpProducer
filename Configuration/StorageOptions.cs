namespace TcpClientDevice.Configuration;

public sealed class StorageOptions
{
	public const string SectionName = "Storage";

	public string Provider { get; set; } = "InMemory";
	public string RedisConnection { get; set; } = "localhost:6379";
}
