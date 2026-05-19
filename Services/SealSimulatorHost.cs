using TcpClientDevice.Configuration;
using TcpClientDevice.Protocol;
using TcpClientDevice.Protocol.Handlers;
using TcpClientDevice.Storage;

namespace TcpClientDevice.Services;

public sealed class SealSimulatorHost : IAsyncDisposable
{
	readonly AppOptions _options;
	readonly ISealStateStore _store;
	readonly bool _disposeStore;
	readonly Action<string> _log;

	public SealSimulatorHost(AppOptions options, ISealStateStore? store, Action<string> log)
	{
		_options = options;
		if (store is not null)
		{
			_store = store;
			_disposeStore = false;
		}
		else
		{
			_store = SealStateStoreFactory.Create(options.Storage);
			_disposeStore = true;
		}

		_log = log;
	}

	public async Task RunAsync(CancellationToken cancellationToken)
	{
		var seals = ResolveSeals();
		_log($"пломб: {seals.Count} | storage: {_options.Storage.Provider} | session {_options.Device.SessionDuration}");

		await StartAllDevicesStaggeredAsync(seals, cancellationToken);
	}

	/// <summary>
	/// Первый запуск всех устройств: каждое стартует со случайной задержкой от 0 до ConnectInterval.
	/// </summary>
	public Task StartAllDevicesStaggeredAsync(
		IReadOnlyList<SealRuntimeConfig> seals,
		CancellationToken cancellationToken)
	{
		var payloadFactory = new SealPayloadFactory(_options.Telemetry, _store);
		var typeResolver = new PacketTypeResolver();

		var tasks = seals.Select(seal =>
			StartDeviceWithRandomInitialDelayAsync(seal, payloadFactory, typeResolver, cancellationToken));

		return Task.WhenAll(tasks);
	}

	async Task StartDeviceWithRandomInitialDelayAsync(
		SealRuntimeConfig seal,
		SealPayloadFactory payloadFactory,
		IPacketTypeResolver typeResolver,
		CancellationToken cancellationToken)
	{
		var maxMs = (int)seal.ConnectInterval.TotalMilliseconds;
		var delayMs = maxMs > 0 ? Random.Shared.Next(0, maxMs + 1) : 0;
		var delay = TimeSpan.FromMilliseconds(delayMs);

		if (delay > TimeSpan.Zero)
			_log($"[{seal.SerialNumber}] первый старт через {delay:g}");

		await Task.Delay(delay, cancellationToken);
		await RunSealAsync(seal, payloadFactory, typeResolver, cancellationToken);
	}

	async Task RunSealAsync(
		SealRuntimeConfig seal,
		SealPayloadFactory payloadFactory,
		IPacketTypeResolver typeResolver,
		CancellationToken cancellationToken)
	{
		var device = new DeviceOptions { SerialNumber = seal.SerialNumber };
		var registry = new ProtocolHandlerRegistry(_options.Protocol, device);
		registry.Register(new OpenSealHandler(_store));

		var router = new ProtocolRouter(typeResolver, registry, device);

		var worker = new SealWorker(
			seal.SerialNumber,
			seal.ConnectInterval,
			_options.Device.SessionDuration,
			seal.PacketInterval,
			_options,
			_store,
			payloadFactory,
			(serial, message) => router.RouteIncomingAsync(serial, message, cancellationToken),
			typeResolver,
			(sn, msg) => _log($"[{sn}] {msg}"));

		await worker.RunAsync(cancellationToken);
	}

	List<SealRuntimeConfig> ResolveSeals()
	{
		if (_options.Seals.Count > 0)
		{
			return _options.Seals
				.Where(s => !string.IsNullOrWhiteSpace(s.SerialNumber))
				.Select(s => new SealRuntimeConfig(
					s.SerialNumber,
					s.ConnectInterval ?? _options.Device.ConnectInterval,
					s.PacketInterval ?? _options.Device.PacketInterval))
				.ToList();
		}

		var path = SealSerialNumbersLoader.ResolvePath(
			_options.Device.SerialNumbersFile,
			AppContext.BaseDirectory);
		var serials = SealSerialNumbersLoader.Load(path);

		if (serials.Count == 0)
			throw new InvalidOperationException($"В файле {_options.Device.SerialNumbersFile} нет серийных номеров");

		return serials
			.Select(sn => new SealRuntimeConfig(
				sn,
				_options.Device.ConnectInterval,
				_options.Device.PacketInterval))
			.ToList();
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposeStore && _store is IAsyncDisposable disposable)
			await disposable.DisposeAsync();
	}

	public sealed record SealRuntimeConfig(
		string SerialNumber,
		TimeSpan ConnectInterval,
		TimeSpan PacketInterval);
}
