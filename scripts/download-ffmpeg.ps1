$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$downloadDirectory = Join-Path $repositoryRoot 'artifacts\downloads'
$archivePath = Join-Path $downloadDirectory 'ffmpeg-release-essentials.zip'
$extractPath = Join-Path $downloadDirectory 'ffmpeg-extracted'
$targetPath = Join-Path $repositoryRoot 'tools\ffmpeg'

New-Item -ItemType Directory -Force -Path $downloadDirectory | Out-Null

if (-not (Test-Path -LiteralPath $archivePath)) {
    Invoke-WebRequest -Uri 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip' -OutFile $archivePath
}

if (-not (Test-Path -LiteralPath $extractPath)) {
    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractPath
}

$binaryDirectory = Get-ChildItem -LiteralPath $extractPath -Directory |
    Select-Object -First 1 |
    ForEach-Object { Join-Path $_.FullName 'bin' }

if (-not $binaryDirectory -or -not (Test-Path -LiteralPath (Join-Path $binaryDirectory 'ffmpeg.exe'))) {
    throw 'The FFmpeg archive layout was not recognized.'
}

New-Item -ItemType Directory -Force -Path $targetPath | Out-Null
Copy-Item -LiteralPath (Join-Path $binaryDirectory 'ffmpeg.exe') -Destination $targetPath -Force
Copy-Item -LiteralPath (Join-Path $binaryDirectory 'ffprobe.exe') -Destination $targetPath -Force

& (Join-Path $targetPath 'ffmpeg.exe') -version | Select-Object -First 1

