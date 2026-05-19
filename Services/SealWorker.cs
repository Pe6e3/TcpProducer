using TcpClientDevice.Configuration;
using TcpClientDevice.Protocol;
using TcpClientDevice.Storage;

namespace TcpClientDevice.Services;

/// <summary>
/// Симуляция одной пломбы: накопление телеметрии offline и сессии online.
/// </summary>
public sealed class SealWorker
{
	static readonly TimeSpan AckRetryDelay = TimeSpan.FromSeconds(5);

	readonly string _serial;
	readonly TimeSpan _connectInterval;
	readonly TimeSpan _sessionDuration;
	readonly TimeSpan _packetInterval;
	readonly AppOptions _options;
	readonly ISealStateStore _store;
	readonly SealPayloadFactory _payloadFactory;
	readonly Func<string, string, Task<HandlerResult>> _routeIncoming;
	readonly IPacketTypeResolver _typeResolver;
	readonly Action<string, string> _log;

	public SealWorker(
		string serialNumber,
		TimeSpan connectInterval,
		TimeSpan sessionDuration,
		TimeSpan packetInterval,
		AppOptions options,
		ISealStateStore store,
		SealPayloadFactory payloadFactory,
		Func<string, string, Task<HandlerResult>> routeIncoming,
		IPacketTypeResolver typeResolver,
		Action<string, string> log)
	{
		_serial = serialNumber;
		_connectInterval = connectInterval;
		_sessionDuration = sessionDuration;
		_packetInterval = packetInterval;
		_options = options;
		_store = store;
		_payloadFactory = payloadFactory;
		_routeIncoming = routeIncoming;
		_typeResolver = typeResolver;
		_log = log;
	}

	public async Task RunAsync(CancellationToken cancellationToken)
	{
		_log(_serial, $"старт | wake {_connectInterval} | session {_sessionDuration} | data {_packetInterval}");

		var accumulator = RunOfflineAccumulatorAsync(cancellationToken);

		while (!cancellationToken.IsCancellationRequested)
		{
			var nextWakeAt = DateTime.UtcNow + _connectInterval;
			await RunOnlineSessionAsync(cancellationToken);

			var delay = nextWakeAt - DateTime.UtcNow;
			if (delay <= TimeSpan.Zero)
				continue;

			try
			{
				await Task.Delay(delay, cancellationToken);
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}

		try
		{
			await accumulator;
		}
		catch (OperationCanceledException)
		{
			// штатная остановка
		}
	}

	async Task RunOfflineAccumulatorAsync(CancellationToken cancellationToken)
	{
		var dataMessage = _options.Protocol.AfterConnect.Message;
		if (!SealPayloadFactory.IsTelemetryConfigured(dataMessage))
			return;

		while (!cancellationToken.IsCancellationRequested)
		{
			var state = await _store.GetStateAsync(_serial, cancellationToken);
			if (!state.IsOnline)
			{
				var packet = await _payloadFactory.CreateTelemetryAsync(_serial, cancellationToken);
				await _store.EnqueueTelemetryAsync(_serial, packet, cancellationToken);
			}

			await Task.Delay(_packetInterval, cancellationToken);
		}
	}

	async Task RunOnlineSessionAsync(CancellationToken cancellationToken)
	{
		await _store.SetOnlineAsync(_serial, true, cancellationToken);

		void WriteLog(string msg) => _log(_serial, msg);

		var sessionTimer = new SessionTimer(_sessionDuration);

		await using var session = new TcpSession(_options.Tcp, WriteLog);
		try
		{
			await session.ConnectAsync(cancellationToken);
			session.StartReceiving(
				msg => OnIncomingAsync(session, msg, sessionTimer, cancellationToken),
				cancellationToken);

			var onConnect = _options.Protocol.OnConnect;
			if (OutboundPayloadFactory.IsConfigured(onConnect))
			{
				var text = _payloadFactory.GetConnectPayload(_serial);
				var bytes = PacketEncoding.Encode(text, onConnect.Encoding);
				WriteLog(PacketLogFormatter.Outbound(onConnect, bytes, text));
				await session.SendAsync(bytes, cancellationToken);
			}

			while (await _store.TryDequeueTelemetryAsync(_serial, cancellationToken) is { } queued)
			{
				if (!await SendTelemetryBytesAsync(session, queued, sessionTimer, cancellationToken))
					break;
			}

			var dataMessage = _options.Protocol.AfterConnect.Message;
			if (SealPayloadFactory.IsTelemetryConfigured(dataMessage))
			{
				var nextPacketAt = DateTime.UtcNow + _packetInterval;

				while (DateTime.UtcNow < sessionTimer.End && !cancellationToken.IsCancellationRequested)
				{
					if (nextPacketAt > DateTime.UtcNow)
						await SessionTimer.DelayUntilAsync(nextPacketAt, sessionTimer, cancellationToken);

					if (DateTime.UtcNow >= sessionTimer.End || cancellationToken.IsCancellationRequested)
						break;

					var live = await _payloadFactory.CreateTelemetryAsync(_serial, cancellationToken);
					if (!await SendTelemetryBytesAsync(session, live, sessionTimer, cancellationToken))
						break;

					nextPacketAt = DateTime.UtcNow + _packetInterval;
				}
			}

			await WaitUntilSessionEndAsync(sessionTimer, cancellationToken);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			WriteLog($"! {ex.Message}");
		}
		finally
		{
			await session.StopReceivingAsync();
			session.Disconnect();
			WriteLog("конец сессии");
			await _store.SetOnlineAsync(_serial, false, cancellationToken);
		}
	}

	static async Task WaitUntilSessionEndAsync(SessionTimer sessionTimer, CancellationToken cancellationToken)
	{
		while (DateTime.UtcNow < sessionTimer.End)
		{
			var remaining = sessionTimer.End - DateTime.UtcNow;
			if (remaining <= TimeSpan.Zero)
				break;

			var chunk = remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1);
			await Task.Delay(chunk, cancellationToken);
		}
	}

	async Task<bool> SendTelemetryBytesAsync(
		TcpSession session,
		byte[] bytes,
		SessionTimer sessionTimer,
		CancellationToken cancellationToken)
	{
		var sentSerial = bytes[^1];
		var ackTimeout = TimeSpan.FromSeconds(30);
		sessionTimer.Extend();

		while (!cancellationToken.IsCancellationRequested && DateTime.UtcNow < sessionTimer.End)
		{
			var pendingInQueue = await _store.GetQueueLengthAsync(_serial, cancellationToken);
			_log(_serial, PacketLogFormatter.Outbound(
				_options.Protocol.AfterConnect.Message,
				bytes,
				null,
				pendingInQueue));

			await session.SendAsync(bytes, cancellationToken);

			var remaining = sessionTimer.End - DateTime.UtcNow;
			if (remaining <= TimeSpan.Zero)
				break;

			var waitTimeout = remaining < ackTimeout ? remaining : ackTimeout;
			var ackResult = await session.WaitForTelemetryAckAsync(
				sentSerial,
				waitTimeout,
				cancellationToken);

			if (ackResult == TelemetryAckWaitResult.Valid)
				return true;

			if (ackResult == TelemetryAckWaitResult.InvalidSerial)
			{
				_log(_serial, $"! ack invalid serial (expected P69 s={sentSerial})");
				return false;
			}

			if (DateTime.UtcNow >= sessionTimer.End)
			{
				_log(_serial, $"! ack timeout (expected P69 s={sentSerial}), сессия завершена");
				return false;
			}

			var retryDelay = AckRetryDelay;
			remaining = sessionTimer.End - DateTime.UtcNow;
			if (retryDelay > remaining)
				retryDelay = remaining;

			_log(_serial, $"! ack timeout (expected P69 s={sentSerial}), повтор через {retryDelay.TotalSeconds:0}с");
			await SessionTimer.DelayUntilAsync(DateTime.UtcNow + retryDelay, sessionTimer, cancellationToken);
		}

		return false;
	}

	async Task OnIncomingAsync(
		TcpSession session,
		string message,
		SessionTimer sessionTimer,
		CancellationToken cancellationToken)
	{
		var packet = _typeResolver.Resolve(message);

		if (ShouldExtendSession(packet))
			sessionTimer.Extend();

		if (packet.PacketType == TelemetryAck.PacketType)
			return;

		if (packet.IsServerCommand)
			_log(_serial, PacketLogFormatter.InboundCommand(packet.RawMessage));
		else if (!string.IsNullOrWhiteSpace(packet.RawMessage))
			_log(_serial, PacketLogFormatter.Inbound(packet.RawMessage));

		var result = await _routeIncoming(_serial, message);
		if (result.ResponsePayload is not { Length: > 0 } response)
			return;

		var text = PacketEncoding.DecodeToDisplay(response, "Ascii");
		_log(_serial, PacketLogFormatter.OutboundResponse(text));
		await session.SendAsync(response, cancellationToken);
	}

	static bool ShouldExtendSession(PacketTypeInfo packet) =>
		packet.PacketType != TelemetryAck.PacketType
		&& (packet.IsServerCommand || !string.IsNullOrWhiteSpace(packet.RawMessage));
}
