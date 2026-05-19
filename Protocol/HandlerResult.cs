namespace TcpClientDevice.Protocol;

public sealed class HandlerResult
{
	public bool Handled { get; init; }
	public byte[]? ResponsePayload { get; init; }
	public string? LogMessage { get; init; }

	public static HandlerResult NotHandled() => new() { Handled = false };

	public static HandlerResult Respond(byte[] payload, string? log = null) =>
		new() { Handled = true, ResponsePayload = payload, LogMessage = log };

	public static HandlerResult Acknowledged(string? log = null) =>
		new() { Handled = true, LogMessage = log };
}
