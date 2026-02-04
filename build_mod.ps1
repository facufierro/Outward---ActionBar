$ErrorActionPreference = "Stop"

$solutionDir = "$PSScriptRoot"
$projectDir = "$solutionDir\ActionUI.Plugin"
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
if (Test-Path "$projectDir\icon.png") {
    Copy-Item "$projectDir\icon.png" -Destination $publishDir
} else {
    Write-Warning "icon.png not found in $projectDir"
}

Write-Host "Zipping to $zipName ..."
$zipPath = "$solutionDir\$zipName"
if (Test-Path $zipPath) { Remove-Item $zipPath }
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath

Write-Host "Build Complete: $zipPath"
