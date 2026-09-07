<#
.SYNOPSIS
  Builds From Golden Combs and packs a ready-to-install mod zip into .\Releases.
.EXAMPLE
  .\build.ps1                       # Release build -> Releases\FromGoldenCombs-<version>.zip
  .\build.ps1 -Configuration Debug
#>
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "FromGoldenCombs\FromGoldenCombs.csproj"

if (-not (Test-Path (Join-Path $root "Directory.Build.props.user"))) {
    Write-Warning "Directory.Build.props.user not found. Copy Directory.Build.props.user.example and set your game path first."
}

dotnet build $project -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$modinfo = Get-Content (Join-Path $root "FromGoldenCombs\modinfo.json") -Raw | ConvertFrom-Json
$version = $modinfo.version
$outDir = Join-Path $root "FromGoldenCombs\bin\$Configuration\Mods\fromgoldencombs"
$releases = Join-Path $root "Releases"
New-Item -ItemType Directory -Force -Path $releases | Out-Null

$zip = Join-Path $releases "FromGoldenCombs-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $outDir "*") -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Created $zip"
