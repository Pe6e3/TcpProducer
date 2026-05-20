using TcpClientDevice.Configuration;
using TcpClientDevice.Services;

var basePath = AppContext.BaseDirectory;
var options = AppConfiguration.Load(basePath);

var serialsPath = SealSerialNumbersLoader.ResolvePath(
	options.Device.SerialNumbersFile,
	basePath);
var hasSeals = options.Seals.Any(s => !string.IsNullOrWhiteSpace(s.SerialNumber));
if (!hasSeals && !File.Exists(serialsPath))
	throw new InvalidOperationException(
		$"Файл серийных номеров не найден: {serialsPath}. Задайте Device:SerialNumbersFile или список Seals");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
	e.Cancel = true;
	cts.Cancel();
};

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;

void Log(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");

PacketStats.Initialize();
PacketStats.StartLivePersistence(cts.Token);

await using var host = new SealSimulatorHost(options, store: null, Log);
await host.RunAsync(cts.Token);
