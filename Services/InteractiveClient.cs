using System.Net.Sockets;
using System.Text;
using TcpClientDevice.Configuration;
using TcpClientDevice.Protocol;
namespace TcpClientDevice.Services;

/// <summary>
/// Ручной режим: ввод в консоли + маршрутизация входящих через ProtocolRouter.
/// </summary>
public sealed class InteractiveClient
{
	readonly AppOptions _options;
	readonly ProtocolRouter _router;
	readonly IPacketTypeResolver _typeResolver;
	readonly Action<string> _log;

	public InteractiveClient(AppOptions options, ProtocolRouter router, IPacketTypeResolver typeResolver, Action<string> log)
	{
		_options = options;
		_router = router;
		_typeResolver = typeResolver;
		_log = log;
	}

	public async Task RunAsync(CancellationToken cancellationToken)
	{
		var tcp = _options.Tcp;
		using var client = new TcpClient();
		Console.WriteLine($"Подключение к {tcp.Host}:{tcp.Port}...");
		await client.ConnectAsync(tcp.Host, tcp.Port, cancellationToken);
		Console.WriteLine("Интерактивный режим. Enter — отправить, exit — выход.");

		var stream = client.GetStream();
		var consoleLock = new object();
		var encoding = Encoding.UTF8;
		var messageBuffer = new IncomingMessageBuffer();

		void LogLocal(string text)
		{
			lock (consoleLock)
				_log(text);
		}

		var receiveTask = Task.Run(async () =>
		{
			var buffer = new byte[8192];
			try
			{
				while (!cancellationToken.IsCancellationRequested)
				{
					var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
					if (read == 0)
					{
						LogLocal("- disconnect");
						break;
					}

					var chunk = encoding.GetString(buffer, 0, read);
					foreach (var message in messageBuffer.Append(chunk))
					{
						var packet = _typeResolver.Resolve(message);
						if (packet.PacketType == TelemetryAck.PacketType)
							LogLocal(PacketLogFormatter.InboundAck(packet.RawMessage));
						else if (packet.IsServerCommand)
							LogLocal(PacketLogFormatter.InboundCommand(packet.RawMessage));
						else
							LogLocal(PacketLogFormatter.Inbound(packet.RawMessage));

						var result = await _router.RouteIncomingAsync(_options.Device.SerialNumber, message, cancellationToken);
						if (result.ResponsePayload is { Length: > 0 } response)
						{
							var responseText = encoding.GetString(response);
							LogLocal(PacketLogFormatter.OutboundResponse(responseText));
							await stream.WriteAsync(response, cancellationToken);
							await stream.FlushAsync(cancellationToken);
						}
					}
				}
			}
			catch (OperationCanceledException) { }
			catch (IOException ex)
			{
				if (!cancellationToken.IsCancellationRequested)
					LogLocal($"! read {ex.Message}");
			}
		}, cancellationToken);

		while (!cancellationToken.IsCancellationRequested)
		{
			string? line = Console.ReadLine();
			if (line is null)
				break;
			if (string.Equals(line, "exit", StringComparison.OrdinalIgnoreCase))
				break;

			var text = tcp.AppendNewline ? line + "\n" : line;
			LogLocal(PacketLogFormatter.OutboundAscii(text.TrimEnd('\r', '\n')));
			var data = encoding.GetBytes(text);
			await stream.WriteAsync(data, cancellationToken);
			await stream.FlushAsync(cancellationToken);
		}

		try
		{
			await receiveTask;
		}
		catch
		{
			// отмена
		}
	}
}
