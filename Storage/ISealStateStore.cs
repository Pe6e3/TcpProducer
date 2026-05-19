using TcpClientDevice.Seals;

namespace TcpClientDevice.Storage;

public interface ISealStateStore
{
	Task<SealState> GetStateAsync(string serialNumber, CancellationToken cancellationToken = default);
	Task SetStatusAsync(string serialNumber, SealStatus status, CancellationToken cancellationToken = default);
	Task SetOnlineAsync(string serialNumber, bool isOnline, CancellationToken cancellationToken = default);
	Task<byte> GetNextAckSerialAsync(string serialNumber, CancellationToken cancellationToken = default);
	Task EnqueueTelemetryAsync(string serialNumber, byte[] packet, CancellationToken cancellationToken = default);
	Task<byte[]?> TryDequeueTelemetryAsync(string serialNumber, CancellationToken cancellationToken = default);
	Task<int> GetQueueLengthAsync(string serialNumber, CancellationToken cancellationToken = default);
}
