$ErrorActionPreference = "Stop"
$dotNetRoot = Join-Path $env:LOCALAPPDATA "dotnet"
if (Test-Path (Join-Path $dotNetRoot "dotnet.exe")) {
    $env:DOTNET_ROOT = $dotNetRoot
    $env:PATH = "$dotNetRoot;$env:PATH"
}
$sln = Join-Path $PSScriptRoot "..\windows-client\PhoneControl.sln"
dotnet build $sln -c Debug
