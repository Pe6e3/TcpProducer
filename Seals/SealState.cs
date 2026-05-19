namespace TcpClientDevice.Seals;

public sealed class SealState
{
	public string SerialNumber { get; init; } = "";
	public SealStatus Status { get; set; } = SealStatus.Closed;
	public bool IsOnline { get; set; }
	public DateTime? OpenedAtUtc { get; set; }
}
