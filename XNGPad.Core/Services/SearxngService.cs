using XNGPad.Core.Models;

namespace XNGPad.Core.Services;

public sealed class SearxngService
{
    public static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan SettingsWait = TimeSpan.FromSeconds(15);

    private readonly DockerService _docker;
    private readonly HttpClient _http;

    public SearxngService(DockerService docker, HttpClient http)
    {
        _docker = docker;
        _http = http;
    }

    public async Task EnsureContainerAsync(AppSettings settings, bool recreate, IProgress<string>? progress, CancellationToken ct = default)
    {
        var name = settings.ResolvedContainerName();
        var port = settings.ResolvedPort();
        var image = Constants.Image;

        if (!await _docker.IsDaemonRunningAsync(ct).ConfigureAwait(false))
            throw new XNGPadException(_docker.IsDesktopProcessRunning() ? "ErrorDockerStarting" : "ErrorWsl");

        var info = await _docker.InspectAsync(name, ct).ConfigureAwait(false);
        if (info.Exists && !DockerService.IsSearxngImage(info.Image))
        {
            if (!recreate)
                throw new ContainerImageConflictException(name, info.Image);
            progress?.Report("BusyStartingSearxng");
            await _docker.RemoveForceAsync(name, ct).ConfigureAwait(false);
            info = new ContainerInfo();
        }

        if (info.Exists)
        {
            if (!info.Running)
            {
                if (TcpPort.IsInUse(port))
                    throw new PortInUseException(port);
                progress?.Report("BusyStartingSearxng");
                await _docker.StartAsync(name, ct).ConfigureAwait(false);
            }
        }
        else
        {
            if (TcpPort.IsInUse(port))
                throw new PortInUseException(port);

            var hasImage = await _docker.ImageExistsAsync(image, ct).ConfigureAwait(false)
                           || await _docker.ImageExistsAsync("searxng/searxng:latest", ct).ConfigureAwait(false);
            if (!hasImage)
            {
                progress?.Report("BusyPulling");
                await _docker.PullAsync(image, ct).ConfigureAwait(false);
            }

            progress?.Report("BusyStartingSearxng");
            await _docker.RunAsync(name, port, image, Constants.VolumeName, ct).ConfigureAwait(false);
        }

        progress?.Report("BusyEnablingJson");
        await EnableJsonFormatAsync(name, ct).ConfigureAwait(false);
    }

    public async Task EnableJsonFormatAsync(string containerName, CancellationToken ct = default)
    {
        var yaml = await WaitForSettingsAsync(containerName, ct).ConfigureAwait(false);
        if (yaml == null)
            throw new XNGPadException("ErrorSettingsYmlWait");

        if (SearxngSettingsPatcher.HasJsonFormat(yaml))
            return;

        var patched = SearxngSettingsPatcher.EnableJsonFormat(yaml);
        var write = await _docker.ExecAsync(
            containerName,
            new[] { "tee", "/etc/searxng/settings.yml" },
            stdin: patched,
            ct: ct).ConfigureAwait(false);
        if (!write.Success)
        {
            write = await _docker.ExecAsync(
                containerName,
                new[] { "sh", "-c", "cat > /etc/searxng/settings.yml" },
                stdin: patched,
                ct: ct).ConfigureAwait(false);
        }

        if (!write.Success)
            throw new XNGPadException("ErrorJson403");

        await _docker.RestartAsync(containerName, ct).ConfigureAwait(false);
    }

    public async Task StopAsync(string containerName, CancellationToken ct = default)
    {
        var info = await _docker.InspectAsync(containerName, ct).ConfigureAwait(false);
        if (!info.Exists)
            throw new XNGPadException("ErrorNoContainer");
        if (info.Running)
            await _docker.StopAsync(containerName, ct).ConfigureAwait(false);
    }

    public async Task<HealthResult> HealthCheckAsync(string baseUrl, CancellationToken ct = default)
    {
        var root = baseUrl.TrimEnd('/');
        try
        {
            using var rootCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            rootCts.CancelAfter(HealthTimeout);
            using var rootResp = await _http.GetAsync(root + "/", rootCts.Token).ConfigureAwait(false);
            if (!rootResp.IsSuccessStatusCode && (int)rootResp.StatusCode is < 200 or >= 500)
                return new HealthResult { Kind = HealthKind.Unreachable, StatusCode = (int)rootResp.StatusCode };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new HealthResult { Kind = HealthKind.Unreachable };
        }
        catch (HttpRequestException)
        {
            return new HealthResult { Kind = HealthKind.Unreachable };
        }

        try
        {
            using var jsonCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            jsonCts.CancelAfter(HealthTimeout);
            var url = root + "/search?q=test&format=json";
            using var resp = await _http.GetAsync(url, jsonCts.Token).ConfigureAwait(false);
            var code = (int)resp.StatusCode;
            if (code == 403)
                return new HealthResult { Kind = HealthKind.JsonForbidden, StatusCode = code };

            var body = await resp.Content.ReadAsStringAsync(jsonCts.Token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                if (LooksLikeHtml(body))
                    return new HealthResult { Kind = HealthKind.JsonForbidden, StatusCode = code };
                return new HealthResult { Kind = HealthKind.Unreachable, StatusCode = code };
            }

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                return new HealthResult { Kind = HealthKind.Ok, StatusCode = code };
            }
            catch (System.Text.Json.JsonException)
            {
                return new HealthResult { Kind = HealthKind.JsonForbidden, StatusCode = code };
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new HealthResult { Kind = HealthKind.Unreachable };
        }
        catch (HttpRequestException)
        {
            return new HealthResult { Kind = HealthKind.Unreachable };
        }
    }

    public async Task<HealthResult> WaitHealthyAsync(string baseUrl, TimeSpan wait, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + wait;
        HealthResult last = new() { Kind = HealthKind.Unreachable };
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            last = await HealthCheckAsync(baseUrl, ct).ConfigureAwait(false);
            if (last.Kind is HealthKind.Ok or HealthKind.JsonForbidden)
                return last;
            await Task.Delay(2000, ct).ConfigureAwait(false);
        }

        return last;
    }

    private async Task<string?> WaitForSettingsAsync(string containerName, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + SettingsWait;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var test = await _docker.ExecAsync(
                containerName,
                new[] { "sh", "-c", "test -f /etc/searxng/settings.yml" },
                ct: ct).ConfigureAwait(false);
            if (test.Success)
            {
                var cat = await _docker.ExecAsync(
                    containerName,
                    new[] { "cat", "/etc/searxng/settings.yml" },
                    ct: ct).ConfigureAwait(false);
                if (cat.Success && !string.IsNullOrWhiteSpace(cat.StandardOutput))
                    return cat.StandardOutput;
            }

            await Task.Delay(1000, ct).ConfigureAwait(false);
        }

        return null;
    }

    private static bool LooksLikeHtml(string body)
    {
        var t = body.TrimStart();
        return t.StartsWith("<", StringComparison.Ordinal) || t.Contains("<html", StringComparison.OrdinalIgnoreCase);
    }
}
