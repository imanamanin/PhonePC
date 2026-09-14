$ErrorActionPreference = "Stop"
$dotNetRoot = Join-Path $env:LOCALAPPDATA "dotnet"
if (Test-Path (Join-Path $dotNetRoot "dotnet.exe")) {
    $env:DOTNET_ROOT = $dotNetRoot
    $env:PATH = "$dotNetRoot;$env:PATH"
}

Write-Output "=== Phone Control Suite environment ==="
Write-Output "dotnet: $(dotnet --version)"
dotnet --list-sdks
Write-Output "git: $(git --version)"

$java = Get-Command java -ErrorAction SilentlyContinue
if ($java) {
    Write-Output "java: $(java -version 2>&1 | Select-Object -First 1)"
} else {
    Write-Output "java: not found (Android build needs JDK 17)"
}

$adb = Get-Command adb -ErrorAction SilentlyContinue
if ($adb) { Write-Output "adb: found" } else { Write-Output "adb: not found (optional until Phase 1)" }

$sdk = $env:ANDROID_HOME
if (-not $sdk -and (Test-Path "$env:LOCALAPPDATA\Android\Sdk")) {
    $sdk = "$env:LOCALAPPDATA\Android\Sdk"
}
if ($sdk) { Write-Output "Android SDK: $sdk" } else { Write-Output "Android SDK: not found" }
