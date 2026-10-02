# Hard timer around dotnet test. On timeout the test process tree is killed
# and the snapshotted files are put back, even if the suite was mid-write.
param(
    [int]$TimeoutSec = 180
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$stamp = Join-Path $env:TEMP ("xngpad-test-snap-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $stamp | Out-Null
$targets = @(
    (Join-Path $env:USERPROFILE ".config\opencode\opencode.json"),
    (Join-Path $env:APPDATA "XNGPad\ufp\schema-proxy.mjs"),
    (Join-Path $env:APPDATA "XNGPad\kompas-mcp\schema-proxy.mjs"),
    (Join-Path $env:APPDATA "XNGPad\telegram\launch.mjs"),
    (Join-Path $env:SystemRoot "System32\drivers\etc\hosts")
)

$manifest = @()
foreach ($path in $targets) {
    $copy = Join-Path $stamp ([guid]::NewGuid().ToString("N"))
    $existed = Test-Path -LiteralPath $path
    if ($existed) {
        Copy-Item -LiteralPath $path -Destination $copy -Force
    }
    $manifest += [pscustomobject]@{ Path = $path; Copy = $copy; Existed = $existed }
}

function Restore-Snapshot {
    foreach ($item in $manifest) {
        if ($item.Existed) {
            $dir = Split-Path -Parent $item.Path
            if (-not (Test-Path -LiteralPath $dir)) {
                New-Item -ItemType Directory -Path $dir | Out-Null
            }
            Copy-Item -LiteralPath $item.Copy -Destination $item.Path -Force
        }
        elseif (Test-Path -LiteralPath $item.Path) {
            Remove-Item -LiteralPath $item.Path -Force -ErrorAction SilentlyContinue
        }
    }
    Remove-Item -LiteralPath $stamp -Recurse -Force -ErrorAction SilentlyContinue
}

$log = Join-Path $stamp "test.log"
$proc = Start-Process -FilePath "dotnet" -ArgumentList @(
    "test", "XNGPad.Tests\XNGPad.Tests.csproj", "-c", "Release", "--nologo"
) -WorkingDirectory $root -PassThru -RedirectStandardOutput $log -RedirectStandardError "$log.err" -WindowStyle Hidden
$finished = $proc.WaitForExit($TimeoutSec * 1000)
if (-not $finished) {
    & taskkill.exe /T /F /PID $proc.Id | Out-Null
    if (Test-Path $log) { Get-Content -LiteralPath $log -Tail 40 }
    Restore-Snapshot
    Write-Error "Tests exceeded ${TimeoutSec}s and were stopped. Snapshot restored."
    exit 124
}
if (Test-Path $log) { Get-Content -LiteralPath $log }
if (Test-Path "$log.err") { Get-Content -LiteralPath "$log.err" }

$code = $proc.ExitCode
Restore-Snapshot
exit $code
