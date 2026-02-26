param(
    [ValidateSet("dev", "release")]
    [string]$Channel = "dev"
)

$ErrorActionPreference = "Stop"

$solutionDir = "$PSScriptRoot"
$projectDir = "$solutionDir"
$projectFile = "$projectDir\fierrof.ActionBar.csproj"
$binDir = "$solutionDir\bin"
$publishDir = "$solutionDir\bin\$Channel\publish"
$manifestPath = "$projectDir\manifest.json"

function Get-Manifest {
    if (-not (Test-Path $manifestPath)) {
        Write-Error "manifest.json not found at $manifestPath"
    }
    return Get-Content $manifestPath | ConvertFrom-Json
}

$manifest = Get-Manifest
$modName = $manifest.name
$author = "fierrof"
$releaseVersion = $manifest.version_number

# Auto-bump patch version
$match = [regex]::Match($releaseVersion, '^(\d+)\.(\d+)\.(\d+)$')
if ($match.Success) {
    $major = [int]$match.Groups[1].Value
    $minor = [int]$match.Groups[2].Value
    $patch = [int]$match.Groups[3].Value
    $patch++
    
    $releaseVersion = "$major.$minor.$patch"
    $manifest.version_number = $releaseVersion
    
    # Save back to manifest
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -Path $manifestPath -Encoding UTF8
    Write-Host "Auto-bumped version to $releaseVersion"
}

$packageVersion = if ($Channel -eq "dev") { "$releaseVersion-dev" } else { $releaseVersion }
$zipName = "$author-$modName-$packageVersion.zip"
$channelBinDir = Join-Path $binDir $Channel

Write-Host "Cleaning '$Channel' directory..."
if (Test-Path $channelBinDir) { Remove-Item $channelBinDir -Recurse -Force }

Write-Host "Rebuilding for $Channel..."
dotnet build $projectFile -c Release

Write-Host "Preparing '$Channel/publish' directory..."
if (-not (Test-Path $channelBinDir)) { New-Item -ItemType Directory -Path $channelBinDir -Force | Out-Null }
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

Write-Host "Copying assets to publish directory..."
Copy-Item "$manifestPath" -Destination $publishDir -Force
Copy-Item "$solutionDir\README.md" -Destination $publishDir -Force
Copy-Item "$solutionDir\CHANGELOG.md" -Destination $publishDir -Force
if (Test-Path "$solutionDir\icon.png") { Copy-Item "$solutionDir\icon.png" -Destination $publishDir -Force }

$pluginDll = "$projectDir\bin\Release\netstandard2.0\fierrof.ActionBar.dll"
if (Test-Path $pluginDll) {
    Copy-Item $pluginDll -Destination $publishDir -Force
} else {
    Write-Error "Cannot find built DLL at $pluginDll"
}

if (-not (Test-Path $channelBinDir)) { New-Item -ItemType Directory -Path $channelBinDir -Force | Out-Null }

$zipPath = "$channelBinDir\$zipName"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host "Zipping package to $zipPath ..."
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($publishDir, $zipPath)

Write-Host "Build complete: $zipPath"
