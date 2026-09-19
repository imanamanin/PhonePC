$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $here "pcphone-ifaces.exe"
$cs = Join-Path $here "PcPhoneIfaces.cs"
$jsonPath = Join-Path $here "pcphone.ifaces.json"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $csc /nologo /t:exe /out:$exe $cs
if ($LASTEXITCODE -ne 0) { throw "csc failed" }
$json = @"
{
  "name": "pcphone.ifaces",
  "description": "PcPhone local IPv4 interfaces for Firefox USB/Wi-Fi discovery",
  "path": "$($exe.Replace('\','\\'))",
  "type": "stdio",
  "allowed_extensions": ["pc-phone@phonepc.local"]
}
"@
Set-Content -Path $jsonPath -Value $json -Encoding ASCII
$reg = "HKCU:\Software\Mozilla\NativeMessagingHosts\pcphone.ifaces"
New-Item -Path $reg -Force | Out-Null
Set-ItemProperty -Path $reg -Name "(default)" -Value $jsonPath
Write-Output "Registered native host: $jsonPath"
