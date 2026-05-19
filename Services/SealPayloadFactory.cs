using TcpClientDevice.Configuration;
using TcpClientDevice.Protocol;
using TcpClientDevice.Protocol.Telemetry;
using TcpClientDevice.Seals;
using TcpClientDevice.Storage;

namespace TcpClientDevice.Services;

public sealed class SealPayloadFactory
{
	readonly TelemetryOptions _telemetry;
	readonly TelemetryPacketBuilder _telemetryBuilder;
	readonly ISealStateStore _store;

	public SealPayloadFactory(TelemetryOptions telemetry, ISealStateStore store)
	{
		_telemetry = telemetry;
		_store = store;
		_telemetryBuilder = new TelemetryPacketBuilder(telemetry);
	}

	public string GetConnectPayload(string serialNumber) => ConnectPacketBuilder.Build(serialNumber);

	public async Task<byte[]> CreateTelemetryAsync(string serialNumber, CancellationToken cancellationToken = default)
	{
		var state = await _store.GetStateAsync(serialNumber, cancellationToken);
		var ackSerial = await _store.GetNextAckSerialAsync(serialNumber, cancellationToken);
		var parameters = TelemetryParameterFactory.CreateForSeal(
			_telemetry,
			serialNumber,
			state,
			DateTime.UtcNow,
			ackSerial);
		return _telemetryBuilder.Build(parameters);
	}

	public static bool IsTelemetryConfigured(OutboundMessageOptions message) => message.UsesTelemetryBuilder;
}
