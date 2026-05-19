using Microsoft.Extensions.Configuration;
using TcpClientDevice.Configuration;
using TcpClientDevice.Protocol;
using TcpClientDevice.Services;

var basePath = AppContext.BaseDirectory;
var configuration = new ConfigurationBuilder()
	.SetBasePath(basePath)
	.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
	.Build();

var options = new AppOptions();
configuration.Bind(options);

if (string.IsNullOrWhiteSpace(options.Tcp.Host))
	throw new InvalidOperationException("В appsettings.json не задан Tcp:Host");
if (options.Tcp.Port is < 1 or > 65535)
	throw new InvalidOperationException("В appsettings.json задайте корректный Tcp:Port (1–65535)");

var hasSeals = options.Seals.Any(s => !string.IsNullOrWhiteSpace(s.SerialNumber));
if (!hasSeals)
{
	var serialsPath = SealSerialNumbersLoader.ResolvePath(
		options.Device.SerialNumbersFile,
		basePath);
	if (!File.Exists(serialsPath))
		throw new InvalidOperationException(
			$"Файл серийных номеров не найден: {serialsPath}. Задайте Device:SerialNumbersFile или список Seals");
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
	e.Cancel = true;
	cts.Cancel();
};

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;

void Log(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");

var mode = options.Protocol.Mode.Trim();
if (mode.Equals("Interactive", StringComparison.OrdinalIgnoreCase))
{
	var typeResolver = new PacketTypeResolver();
	var handlerRegistry = new ProtocolHandlerRegistry(options.Protocol, options.Device);
	var router = new ProtocolRouter(typeResolver, handlerRegistry, options.Device);
	var interactive = new InteractiveClient(options, router, typeResolver, Log);
	await interactive.RunAsync(cts.Token);
}
else
{
	await using var host = new SealSimulatorHost(options, store: null, Log);
	await host.RunAsync(cts.Token);
}
