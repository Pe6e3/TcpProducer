using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace TcpProducer.Admin;

public static class LogStreamer
{
	public static async IAsyncEnumerable<string> StreamServiceLogsAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var psi = new ProcessStartInfo
		{
			FileName = "journalctl",
			Arguments = $"-u {AdminOptions.ServiceName} -f -n 200 --no-pager -o cat",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		using var process = new Process { StartInfo = psi };
		process.Start();
		process.ErrorDataReceived += (_, e) =>
		{
			if (!string.IsNullOrEmpty(e.Data))
				// stderr journalctl — редко, пропускаем в поток
				_ = e.Data;
		};

		try
		{
			await using var stream = process.StandardOutput.BaseStream;
			using var reader = new StreamReader(stream, Encoding.UTF8);

			while (!cancellationToken.IsCancellationRequested)
			{
				var line = await reader.ReadLineAsync(cancellationToken);
				if (line is null)
					break;

				yield return line;
			}
		}
		finally
		{
			if (!process.HasExited)
			{
				try
				{
					process.Kill(entireProcessTree: true);
				}
				catch
				{
					// процесс уже завершён
				}
			}
		}
	}

	public static string FormatSseEvent(string line)
	{
		var safe = line.Replace("\r", string.Empty).Replace("\n", " ");
		return $"data: {safe}\n\n";
	}
}
