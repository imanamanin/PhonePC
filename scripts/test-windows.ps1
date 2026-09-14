$ErrorActionPreference = "Stop"
$dotNetRoot = Join-Path $env:LOCALAPPDATA "dotnet"
if (Test-Path (Join-Path $dotNetRoot "dotnet.exe")) {
    $env:DOTNET_ROOT = $dotNetRoot
    $env:PATH = "$dotNetRoot;$env:PATH"
}
$tests = Join-Path $PSScriptRoot "..\windows-client\src\PhoneControl.Tests\PhoneControl.Tests.csproj"
dotnet test $tests -c Debug --filter "Category!=Hardware"
