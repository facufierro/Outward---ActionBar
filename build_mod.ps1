$ErrorActionPreference = "Stop"

$solutionDir = "$PSScriptRoot"
$projectDir = "$solutionDir\ActionUI.Plugin"
$publishDir = "$projectDir\bin\Debug\netstandard2.0\publish"
$zipName = "fierrof-ActionUI_Modified-1.2.0.zip"

Write-Host "Cleaning bin and obj folders..."
Get-ChildItem -Path $solutionDir -Include bin,obj -Recurse | Remove-Item -Recurse -Force

Write-Host "Building and Publishing..."
dotnet publish "$projectDir\ModifAmorphic.Outward.ActionUI.Plugin.csproj" -c Debug

Write-Host "Copying Assets..."
New-Item -ItemType Directory -Path $publishDir -Force
Copy-Item "$projectDir\manifest.json" -Destination $publishDir
if (Test-Path "$projectDir\icon.png") {
    Copy-Item "$projectDir\icon.png" -Destination $publishDir
} else {
    Write-Warning "icon.png not found in $projectDir"
}

Write-Host "Zipping..."
if (Test-Path $zipName) { Remove-Item $zipName }
Compress-Archive -Path "$publishDir\*" -DestinationPath "$solutionDir\$zipName"

Write-Host "Build Complete: $solutionDir\$zipName"
