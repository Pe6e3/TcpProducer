namespace TcpClientDevice.Services;

/// <summary>
/// Конец online-сессии; продлевается при активности сервера.
/// </summary>
public sealed class SessionTimer
{
	readonly TimeSpan _duration;
	readonly object _lock = new();
	DateTime _end;

	public SessionTimer(TimeSpan duration)
	{
		_duration = duration;
		_end = DateTime.UtcNow + duration;
	}

	public DateTime End
	{
		get
		{
			lock (_lock)
				return _end;
		}
	}

	public void Extend()
	{
		lock (_lock)
			_end = DateTime.UtcNow + _duration;
	}

	/// <summary>
	/// Ждёт до <paramref name="until"/>, но прерывается при истечении сессии.
	/// </summary>
	public static async Task DelayUntilAsync(
		DateTime until,
		SessionTimer sessionTimer,
		CancellationToken cancellationToken)
	{
		while (DateTime.UtcNow < until && DateTime.UtcNow < sessionTimer.End)
		{
			var remaining = until - DateTime.UtcNow;
			var sessionRemaining = sessionTimer.End - DateTime.UtcNow;
			if (sessionRemaining < remaining)
				remaining = sessionRemaining;

			if (remaining <= TimeSpan.Zero)
				break;

			var chunk = remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1);
			await Task.Delay(chunk, cancellationToken);
		}
	}
}
