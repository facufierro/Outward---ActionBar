$ErrorActionPreference = "Stop"

$solutionDir = "$PSScriptRoot"
$projectDir = "$solutionDir\ActionUI.Plugin"
$binDir = "$solutionDir\bin"
$tempDir = "$binDir\temp"
$publishDir = "$projectDir\bin\Debug\netstandard2.0\publish"

# Read version from manifest
$manifestPath = "$projectDir\manifest.json"
if (-not (Test-Path $manifestPath)) {
    Write-Error "manifest.json not found at $manifestPath"
}
$manifest = Get-Content $manifestPath | ConvertFrom-Json
$version = $manifest.version_number
$modName = "ActionBar"
$author = "fierrof"
$zipName = "$author-$modName-$version.zip"

Write-Host "Cleaning bin and obj folders..."
Get-ChildItem -Path $solutionDir -Include bin,obj -Recurse | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Building and Publishing..."
dotnet publish "$projectDir\ModifAmorphic.Outward.ActionUI.Plugin.csproj" -c Debug

Write-Host "Copying Assets..."
if (-not (Test-Path $publishDir)) {
    New-Item -ItemType Directory -Path $publishDir -Force
}
Copy-Item "$projectDir\manifest.json" -Destination $publishDir

# Copy icon from Thunderstore/Assets folder
$iconSource = "$solutionDir\Thunderstore\Assets\icon.png"
if (Test-Path $iconSource) {
    Copy-Item $iconSource -Destination $publishDir
} else {
    Write-Warning "icon.png not found at $iconSource"
}

# Copy asset bundle
$assetBundleSource = "$solutionDir\Assets\asset-bundles\action-ui"
if (Test-Path $assetBundleSource) {
    Copy-Item $assetBundleSource -Destination $publishDir
    Write-Host "Copied asset bundle: action-ui"
} else {
    Write-Error "Asset bundle not found at $assetBundleSource"
}

# Copy Profiles folder
$profilesSource = "$solutionDir\Assets\Profiles"
if (Test-Path $profilesSource) {
    Copy-Item $profilesSource -Destination $publishDir -Recurse
    Write-Host "Copied Profiles directory"
} else {
    Write-Warning "Profiles directory not found at $profilesSource"
}

Write-Host "Preparing output directories..."
if (-not (Test-Path $binDir)) {
    New-Item -ItemType Directory -Path $binDir -Force
}
if (-not (Test-Path $tempDir)) {
    New-Item -ItemType Directory -Path $tempDir -Force
}

# Verify DLL exists
$dllPath = "$publishDir\ModifAmorphic.Outward.ActionUI.Plugin.dll"
if (-not (Test-Path $dllPath)) {
    Write-Host "Contents of publish dir:"
    Get-ChildItem $publishDir | Select-Object Name
    Write-Error "Main DLL not found at $dllPath"
} else {
    Write-Host "Found Main DLL at $dllPath"
}
$libPath = "$publishDir\ModifAmorphic.Outward.ActionUI.dll"
if (-not (Test-Path $libPath)) {
     Write-Warning "Library DLL not found at $libPath - Is this expected?"
}

Write-Host "Zipping to $zipName ..."
$zipPath = "$binDir\$zipName"
if (Test-Path $zipPath) { Remove-Item $zipPath }
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath

Write-Host "Build Complete: $zipPath"
