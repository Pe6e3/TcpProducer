using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace TcpProducer.Admin;

public sealed class EditableConfigDto
{
	[JsonPropertyName("device")]
	public DeviceConfigDto Device { get; set; } = new();
}

public sealed class DeviceConfigDto
{
	[JsonPropertyName("maxDevices")]
	public int MaxDevices { get; set; }

	[JsonPropertyName("connectInterval")]
	public string ConnectInterval { get; set; } = "00:02:00";

	[JsonPropertyName("packetInterval")]
	public string PacketInterval { get; set; } = "00:02:00";

	[JsonPropertyName("sessionDuration")]
	public string SessionDuration { get; set; } = "00:00:10";
}

public sealed class ConfigSaveResult
{
	[JsonPropertyName("ok")]
	public bool Ok { get; init; }

	[JsonPropertyName("message")]
	public string Message { get; init; } = "";

	[JsonPropertyName("restarted")]
	public bool Restarted { get; init; }
}

public static class AppConfigService
{
	static readonly string SourcePath = Path.Combine(AdminOptions.ProjectRoot, "appsettings.json");
	static readonly string PublishPath = Path.Combine(AdminOptions.PublishDir, "appsettings.json");

	static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

	public static async Task<EditableConfigDto> ReadEditableAsync(CancellationToken cancellationToken = default)
	{
		var root = await ReadRootAsync(cancellationToken);
		return MapFromRoot(root);
	}

	public static async Task<ConfigSaveResult> SaveAsync(
		EditableConfigDto config,
		bool restart,
		CancellationToken cancellationToken = default)
	{
		Validate(config);

		var root = await ReadRootAsync(cancellationToken);
		ApplyToRoot(root, config);

		var json = root.ToJsonString(WriteOptions);
		await WriteFileAsync(SourcePath, json, cancellationToken);
		await WriteFileAsync(PublishPath, json, cancellationToken);

		if (!restart)
		{
			return new ConfigSaveResult
			{
				Ok = true,
				Message = "Конфигурация сохранена. Перезапустите tcpproducer для применения.",
			};
		}

		var result = await ServiceManager.RestartAsync(cancellationToken);
		var restarted = result.ExitCode == 0;

		return new ConfigSaveResult
		{
			Ok = restarted,
			Restarted = restarted,
			Message = restarted
				? "Конфигурация сохранена, сервис tcpproducer перезапущен."
				: $"Конфигурация сохранена, но перезапуск не удался: {result.Output}",
		};
	}

	static async Task<JsonObject> ReadRootAsync(CancellationToken cancellationToken)
	{
		if (!File.Exists(SourcePath))
			throw new FileNotFoundException($"Не найден {SourcePath}");

		var json = await File.ReadAllTextAsync(SourcePath, cancellationToken);
		return JsonNode.Parse(json)?.AsObject()
			?? throw new InvalidOperationException("Некорректный appsettings.json");
	}

	static EditableConfigDto MapFromRoot(JsonObject root)
	{
		var device = root["Device"]?.AsObject();

		return new EditableConfigDto
		{
			Device = new DeviceConfigDto
			{
				MaxDevices = device?["MaxDevices"]?.GetValue<int>() ?? 0,
				ConnectInterval = device?["ConnectInterval"]?.GetValue<string>() ?? "00:02:00",
				PacketInterval = device?["PacketInterval"]?.GetValue<string>() ?? "00:02:00",
				SessionDuration = device?["SessionDuration"]?.GetValue<string>() ?? "00:00:10",
			},
		};
	}

	static void ApplyToRoot(JsonObject root, EditableConfigDto config)
	{
		var device = root["Device"] as JsonObject ?? new JsonObject();

		device["MaxDevices"] = config.Device.MaxDevices;
		device["ConnectInterval"] = config.Device.ConnectInterval;
		device["PacketInterval"] = config.Device.PacketInterval;
		device["SessionDuration"] = config.Device.SessionDuration;

		if (device["SerialNumbersFile"] is null)
			device["SerialNumbersFile"] = "deviceserials.txt";

		root["Device"] = device;
	}

	static void Validate(EditableConfigDto config)
	{
		if (config.Device.MaxDevices is < 0 or > 50_000)
			throw new ArgumentException("MaxDevices: от 0 (все) до 50000");

		ValidateTimeSpan(config.Device.ConnectInterval, "Device.ConnectInterval");
		ValidateTimeSpan(config.Device.PacketInterval, "Device.PacketInterval");
		ValidateTimeSpan(config.Device.SessionDuration, "Device.SessionDuration");
	}

	static void ValidateTimeSpan(string value, string field)
	{
		if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var ts) || ts < TimeSpan.Zero)
			throw new ArgumentException($"{field}: укажите интервал в формате чч:мм:сс (например 01:00:00)");
	}

	static async Task WriteFileAsync(string path, string json, CancellationToken cancellationToken)
	{
		var temp = path + ".tmp";
		await File.WriteAllTextAsync(temp, json, cancellationToken);
		File.Move(temp, path, overwrite: true);
	}
}
