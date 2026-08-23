<#
.SYNOPSIS
  Build a Thunderstore-ready zip of the mod.

.DESCRIPTION
  Thunderstore wants manifest.json, icon.png and README.md at the root of the zip,
  with the plugin under plugins/. Getting that layout wrong produces a package
  that uploads happily and then installs to the wrong place, so it is worth a
  script rather than a habit.

  README.md is what Thunderstore renders on the mod page, so it is written for
  players. BUILDING.md and DESIGN.md are for contributors and stay out of the
  package - nobody installing a mod needs to be told how to point MSBuild at
  their game folder.

  Builds Release first, then assembles from the build output - never from
  whatever happens to be sitting in a deploy folder.

  Nothing from the game is copied. The zip contains this mod and its metadata.

.PARAMETER OutputDir
  Where to write the zip. Defaults to dist/ beside the repo.

.PARAMETER IncludeSymbols
  Include the .pdb. Useful for a test build shared with someone debugging;
  leave it out of a public release.

.EXAMPLE
  ./tools/Package.ps1
#>
[CmdletBinding()]
param(
    [string]$OutputDir,
    [switch]$IncludeSymbols
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutputDir) { $OutputDir = Join-Path $repo "dist" }

# The manifest is the source of truth for the version, so the zip can never
# disagree with what Thunderstore will read out of it.
$manifestPath = Join-Path $repo "manifest.json"
if (-not (Test-Path $manifestPath)) { throw "No manifest.json at '$manifestPath'." }

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$version = $manifest.version_number
$name = $manifest.name

Write-Host "Packaging $name $version"

# Warn rather than fail: BuildInfo drives the assembly's own version, and a
# mismatch is confusing later without being fatal now.
$buildInfo = Join-Path $repo "Stavebound/BuildInfo.cs"
if (Test-Path $buildInfo) {
    $declared = (Select-String -Path $buildInfo -Pattern 'Version\s*=\s*"([^"]+)"').Matches.Groups[1].Value
    if ($declared -ne $version) {
        Write-Warning "manifest.json says $version but BuildInfo.cs says $declared. They should match."
    }
}

Write-Host "Building Release..."
& dotnet build (Join-Path $repo "Stavebound.sln") -c Release -v minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed; nothing packaged." }

$dll = Join-Path $repo "Stavebound/bin/Release/Stavebound.dll"
if (-not (Test-Path $dll)) { throw "No Stavebound.dll at '$dll'." }

$staging = Join-Path ([System.IO.Path]::GetTempPath()) "stave-package-$version"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $staging "plugins") -Force | Out-Null

Copy-Item $manifestPath $staging
Copy-Item (Join-Path $repo "icon.png") $staging
Copy-Item (Join-Path $repo "README.md") $staging
Copy-Item (Join-Path $repo "CHANGELOG.md") $staging -ErrorAction SilentlyContinue
Copy-Item (Join-Path $repo "LICENSE") $staging -ErrorAction SilentlyContinue
Copy-Item $dll (Join-Path $staging "plugins")

if ($IncludeSymbols) {
    Copy-Item (Join-Path $repo "Stavebound/bin/Release/Stavebound.pdb") (Join-Path $staging "plugins") -ErrorAction SilentlyContinue
}

if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }
$zip = Join-Path $OutputDir "$name-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }

Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zip
Remove-Item $staging -Recurse -Force

Write-Host ""
Write-Host "Wrote $zip"
Get-ChildItem $zip | Select-Object Name, @{ Name = "KB"; Expression = { [math]::Round($_.Length / 1KB, 1) } }
