using System.Diagnostics;
using System.Text;

namespace TcpProducer.Admin;

public sealed class CommandRunner
{
	public async Task<CommandResult> RunAsync(
		string fileName,
		string arguments,
		string? workingDirectory = null,
		CancellationToken cancellationToken = default)
	{
		var psi = new ProcessStartInfo
		{
			FileName = fileName,
			Arguments = arguments,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		if (!string.IsNullOrWhiteSpace(workingDirectory))
			psi.WorkingDirectory = workingDirectory;

		using var process = new Process { StartInfo = psi };
		var output = new StringBuilder();

		process.OutputDataReceived += (_, e) =>
		{
			if (e.Data is not null)
				output.AppendLine(e.Data);
		};
		process.ErrorDataReceived += (_, e) =>
		{
			if (e.Data is not null)
				output.AppendLine(e.Data);
		};

		process.Start();
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();

		await process.WaitForExitAsync(cancellationToken);

		return new CommandResult(process.ExitCode, output.ToString().TrimEnd());
	}

	public async Task<CommandResult> DeployAsync(CancellationToken cancellationToken = default)
	{
		var output = new StringBuilder();
		var steps = new (string File, string Args)[]
		{
			("git", "pull"),
			("dotnet", $"publish -c Release -o {AdminOptions.PublishDir}"),
			("cp", $"{AdminOptions.ProjectRoot}/.env {AdminOptions.PublishDir}/.env"),
			("systemctl", $"restart {AdminOptions.ServiceName}"),
		};

		foreach (var (file, args) in steps)
		{
			output.AppendLine($"$ {file} {args}");
			var result = await RunAsync(file, args, AdminOptions.ProjectRoot, cancellationToken);
			if (!string.IsNullOrWhiteSpace(result.Output))
				output.AppendLine(result.Output);

			if (result.ExitCode != 0)
			{
				output.AppendLine($"Ошибка: код {result.ExitCode}");
				return new CommandResult(result.ExitCode, output.ToString().TrimEnd());
			}

			output.AppendLine();
		}

		return new CommandResult(0, output.ToString().TrimEnd());
	}
}

public readonly record struct CommandResult(int ExitCode, string Output);
