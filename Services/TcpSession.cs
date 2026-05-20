using System.Net.Sockets;
using System.Text;
using TcpClientDevice.Configuration;
using TcpClientDevice.Protocol;

namespace TcpClientDevice.Services;

public sealed class TcpSession : IAsyncDisposable
{
	readonly TcpOptions _tcp;
	readonly Action<string> _log;
	readonly Encoding _textEncoding = Encoding.UTF8;
	readonly IncomingMessageBuffer _buffer = new();
	readonly object _receiveLock = new();

	NetworkStream? _stream;
	TcpClient? _client;
	Task? _receiveTask;
	CancellationTokenSource? _receiveCts;
	Func<string, Task>? _onMessage;

	public bool IsConnected => _client?.Connected == true;

	public TcpSession(TcpOptions tcp, Action<string> log)
	{
		_tcp = tcp;
		_log = log;
	}

	public async Task ConnectAsync(CancellationToken cancellationToken)
	{
		_client = new TcpClient();
		await _client.ConnectAsync(_tcp.Host, _tcp.Port, cancellationToken);
		_stream = _client.GetStream();
		_log($"+ {_tcp.Host}:{_tcp.Port}");
	}

	public void StartReceiving(Func<string, Task> onMessage, CancellationToken cancellationToken)
	{
		_onMessage = onMessage;
		_receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		_receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), CancellationToken.None);
	}

	public async Task SendAsync(byte[] payload, CancellationToken cancellationToken)
	{
		if (_stream is null)
			throw new InvalidOperationException("Нет активного соединения");

		await _stream.WriteAsync(payload, cancellationToken);
		await _stream.FlushAsync(cancellationToken);
		PacketStats.RecordSent();

		if (_tcp.AppendNewline)
		{
			var newline = _textEncoding.GetBytes(Environment.NewLine);
			await _stream.WriteAsync(newline, cancellationToken);
			await _stream.FlushAsync(cancellationToken);
		}
	}

	public Task<string?> WaitForMessageAsync(
		ExpectedAckPattern? expectedAck,
		TimeSpan timeout,
		CancellationToken cancellationToken) =>
		WaitForMessageAsync(
			msg => AckMatcher.Matches(msg, expectedAck),
			timeout,
			cancellationToken);

	public async Task<string?> WaitForMessageAsync(
		Func<string, bool> predicate,
		TimeSpan timeout,
		CancellationToken cancellationToken)
	{
		var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(timeout);

		void Handler(string message)
		{
			if (predicate(message))
				tcs.TrySetResult(message);
		}

		MessageReceived += Handler;
		try
		{
			await using var reg = timeoutCts.Token.Register(() => tcs.TrySetCanceled(timeoutCts.Token));
			return await tcs.Task;
		}
		catch (OperationCanceledException)
		{
			return null;
		}
		finally
		{
			MessageReceived -= Handler;
		}
	}

	public event Action<string>? MessageReceived;

	public async Task<TelemetryAckWaitResult> WaitForTelemetryAckAsync(
		byte expectedSerial,
		TimeSpan timeout,
		CancellationToken cancellationToken)
	{
		var tcs = new TaskCompletionSource<TelemetryAckWaitResult>(
			TaskCreationOptions.RunContinuationsAsynchronously);
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(timeout);

		void Handler(string message)
		{
			if (!TelemetryAck.TryParseSerial(message, out var serial))
				return;

			if (serial == expectedSerial)
				tcs.TrySetResult(TelemetryAckWaitResult.Valid);
			else
				tcs.TrySetResult(TelemetryAckWaitResult.InvalidSerial);
		}

		MessageReceived += Handler;
		try
		{
			await using var reg = timeoutCts.Token.Register(() =>
				tcs.TrySetResult(TelemetryAckWaitResult.Timeout));
			return await tcs.Task;
		}
		finally
		{
			MessageReceived -= Handler;
		}
	}


	async Task ReceiveLoopAsync(CancellationToken cancellationToken)
	{
		if (_stream is null)
			return;

		var readBuffer = new byte[8192];
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				var read = await _stream.ReadAsync(readBuffer.AsMemory(0, readBuffer.Length), cancellationToken);
				if (read == 0)
				{
					_log("- disconnect");
					break;
				}

				var chunk = _textEncoding.GetString(readBuffer, 0, read);
				IEnumerable<string> messages;
				lock (_receiveLock)
					messages = _buffer.Append(chunk).ToArray();

				foreach (var message in messages)
				{
					MessageReceived?.Invoke(message);

					if (_onMessage is not null)
						await _onMessage(message);
				}
			}
		}
		catch (OperationCanceledException)
		{
			// штатная отмена
		}
		catch (IOException ex)
		{
			if (!cancellationToken.IsCancellationRequested)
				_log($"! read {ex.Message}");
		}
	}

	public async Task StopReceivingAsync()
	{
		if (_receiveCts is not null)
		{
			await _receiveCts.CancelAsync();
			if (_receiveTask is not null)
			{
				try
				{
					await _receiveTask;
				}
				catch
				{
					// отмена
				}
			}

			_receiveCts.Dispose();
			_receiveCts = null;
			_receiveTask = null;
		}
	}

	public void Disconnect()
	{
		_stream?.Close();
		_client?.Close();
		_stream = null;
		_client = null;
	}

	public async ValueTask DisposeAsync()
	{
		await StopReceivingAsync();
		Disconnect();
	}
}
