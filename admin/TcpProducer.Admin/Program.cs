using System.Text.Json;

namespace TcpProducer.Admin;

public sealed class AdminAuthMiddleware(RequestDelegate next)
{
	public async Task InvokeAsync(HttpContext context)
	{
		if (IsPublicPath(context.Request.Path))
		{
			await next(context);
			return;
		}

		if (!context.Request.Headers.TryGetValue("Authorization", out var header)
			|| !header.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
		{
			await WriteUnauthorized(context);
			return;
		}

		var token = header.ToString()["Bearer ".Length..].Trim();
		if (!string.Equals(token, AdminOptions.ApiToken, StringComparison.Ordinal))
		{
			await WriteUnauthorized(context);
			return;
		}

		await next(context);
	}

	static bool IsPublicPath(PathString path) =>
		path.StartsWithSegments("/api/health")
		|| path == "/"
		|| path.StartsWithSegments("/index.html")
		|| path.StartsWithSegments("/app.js")
		|| path.StartsWithSegments("/styles.css");

	static Task WriteUnauthorized(HttpContext context)
	{
		context.Response.StatusCode = StatusCodes.Status401Unauthorized;
		return context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
	}
}

public static class Program
{
	public static void Main(string[] args)
	{
		AdminOptions.Load();

		var builder = WebApplication.CreateBuilder(args);
		var app = builder.Build();

		app.UseMiddleware<AdminAuthMiddleware>();
		app.UseDefaultFiles();
		app.UseStaticFiles();

		app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

		app.MapGet("/api/stats", async (CancellationToken ct) =>
			Results.Ok(await StatsReader.ReadAsync(ct)));

		app.MapGet("/api/stats/stream", async (HttpContext context, CancellationToken ct) =>
		{
			context.Response.Headers.CacheControl = "no-cache, no-store";
			context.Response.Headers.Connection = "keep-alive";
			context.Response.ContentType = "text/event-stream";

			await context.Response.StartAsync(ct);

			await foreach (var payload in StatsStreamer.StreamAsync(ct))
			{
				await context.Response.WriteAsync(payload, ct);
				await context.Response.Body.FlushAsync(ct);
			}
		});

		app.MapGet("/api/config", async (CancellationToken ct) =>
		{
			try
			{
				return Results.Ok(await AppConfigService.ReadEditableAsync(ct));
			}
			catch (Exception ex)
			{
				return Results.BadRequest(new { error = ex.Message });
			}
		});

		app.MapGet("/api/serials", async (CancellationToken ct) =>
		{
			try
			{
				return Results.Ok(await SerialsFileService.ReadAsync(ct));
			}
			catch (Exception ex)
			{
				return Results.BadRequest(new { error = ex.Message });
			}
		});

		app.MapPut("/api/serials", async (SerialsSaveRequest request, bool restart, CancellationToken ct) =>
		{
			try
			{
				var result = await SerialsFileService.SaveAsync(request.Content, restart, ct);
				return result.Ok ? Results.Ok(result) : Results.BadRequest(result);
			}
			catch (Exception ex)
			{
				return Results.BadRequest(new { ok = false, message = ex.Message });
			}
		});

		app.MapPut("/api/config", async (EditableConfigDto config, bool restart, CancellationToken ct) =>
		{
			try
			{
				var result = await AppConfigService.SaveAsync(config, restart, ct);
				return result.Ok ? Results.Ok(result) : Results.BadRequest(result);
			}
			catch (Exception ex)
			{
				return Results.BadRequest(new { ok = false, message = ex.Message });
			}
		});

		app.MapGet("/api/status", async (CancellationToken ct) =>
		{
			var status = await ServiceManager.GetStatusAsync(ct);
			return Results.Ok(status);
		});

		app.MapPost("/api/start", async (CancellationToken ct) =>
		{
			var result = await ServiceManager.StartAsync(ct);
			return Results.Json(new ActionResponse
			{
				Ok = result.ExitCode == 0,
				Output = result.Output,
			});
		});

		app.MapPost("/api/stop", async (CancellationToken ct) =>
		{
			var result = await ServiceManager.StopAsync(ct);
			return Results.Json(new ActionResponse
			{
				Ok = result.ExitCode == 0,
				Output = result.Output,
			});
		});

		app.MapPost("/api/deploy", async (CancellationToken ct) =>
		{
			var result = await ServiceManager.DeployAsync(ct);
			return Results.Json(new ActionResponse
			{
				Ok = result.ExitCode == 0,
				Output = result.Output,
			});
		});

		app.MapGet("/api/logs/stream", async (HttpContext context, CancellationToken ct) =>
		{
			context.Response.Headers.CacheControl = "no-cache, no-store";
			context.Response.Headers.Connection = "keep-alive";
			context.Response.ContentType = "text/event-stream";

			await context.Response.StartAsync(ct);

			await foreach (var line in LogStreamer.StreamServiceLogsAsync(ct))
			{
				var payload = LogStreamer.FormatSseEvent(line);
				await context.Response.WriteAsync(payload, ct);
				await context.Response.Body.FlushAsync(ct);
			}
		});

		app.Run("http://127.0.0.1:8770");
	}
}
