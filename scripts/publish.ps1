$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $repositoryRoot '.dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { (Get-Command dotnet -ErrorAction Stop).Source }
$project = Join-Path $repositoryRoot 'src\AudioSlicer\AudioSlicer.csproj'
$publishRoot = Join-Path $repositoryRoot 'publish'
[xml]$projectXml = Get-Content -Raw -LiteralPath $project
$version = [string]($projectXml.Project.PropertyGroup.Version | Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($version)) { throw 'Project version was not found in AudioSlicer.csproj.' }
$packageName = "AudioSlicer-win-x64-v$version"
$output = Join-Path $publishRoot $packageName
$zip = Join-Path $publishRoot "$packageName.zip"

if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot 'tools\ffmpeg\ffmpeg.exe'))) {
    & (Join-Path $PSScriptRoot 'download-ffmpeg.ps1')
}

New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null
& $dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o $output
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'USER_GUIDE.zh-CN.md') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'THIRD_PARTY_NOTICES.md') -Destination $output -Force

if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Output $zip
