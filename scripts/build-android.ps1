$ErrorActionPreference = "Stop"
$agent = Join-Path $PSScriptRoot "..\android-agent"
if (-not (Get-Command java -ErrorAction SilentlyContinue)) {
    Write-Error "JDK 17 is required. Install Android Studio or a JDK, then retry."
}
$wrapper = Join-Path $agent "gradlew.bat"
if (-not (Test-Path $wrapper)) {
    Write-Error "Gradle wrapper is missing. Open android-agent in Android Studio once to generate it."
}
Set-Location $agent
& $wrapper :app:assembleDebug :core-domain:test
