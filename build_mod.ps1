param(
    [ValidateSet("build", "bump", "set-version")]
    [string]$Action = "build",

    [ValidateSet("dev", "release")]
    [string]$Channel = "dev",

    [ValidateSet("patch", "minor", "major")]
    [string]$Bump = "patch",

    [string]$Version,

    [switch]$BuildAfterChange
)

$ErrorActionPreference = "Stop"

$solutionDir = "$PSScriptRoot"
$projectDir = "$solutionDir\ActionUI.Plugin"
$projectFile = "$projectDir\ModifAmorphic.Outward.ActionUI.Plugin.csproj"
$binDir = "$solutionDir\bin"
$publishDir = "$solutionDir\bin\Debug\publish"
$manifestPath = "$projectDir\manifest.json"
$devStatePath = "$solutionDir\.dev-version.json"

function Get-Manifest {
    if (-not (Test-Path $manifestPath)) {
        Write-Error "manifest.json not found at $manifestPath"
    }

    return Get-Content $manifestPath | ConvertFrom-Json
}

function Save-Manifest($manifest) {
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -Path $manifestPath -Encoding UTF8
}

function Get-SemVerParts([string]$value) {
    $match = [regex]::Match($value, '^(\d+)\.(\d+)\.(\d+)$')
    if (-not $match.Success) {
        Write-Error "Version must be strict SemVer core (x.y.z). Current value: '$value'"
    }

    return @(
        [int]$match.Groups[1].Value,
        [int]$match.Groups[2].Value,
        [int]$match.Groups[3].Value
    )
}

function Get-BumpedVersion([string]$currentVersion, [string]$bumpType) {
    $parts = Get-SemVerParts -value $currentVersion
    $major = $parts[0]
    $minor = $parts[1]
    $patch = $parts[2]

    switch ($bumpType) {
        "major" {
            $major++
            $minor = 0
            $patch = 0
        }
        "minor" {
            $minor++
            $patch = 0
        }
        "patch" {
            $patch++
        }
    }

    return "$major.$minor.$patch"
}

function Get-NextDevVersion([string]$releaseVersion) {
    [void](Get-SemVerParts -value $releaseVersion)

    $baseVersion = $releaseVersion
    if (Test-Path $devStatePath) {
        $state = Get-Content $devStatePath | ConvertFrom-Json
        if ($state -and $state.release_version -eq $releaseVersion -and -not [string]::IsNullOrWhiteSpace($state.dev_version)) {
            $baseVersion = $state.dev_version
        }
    }

    $nextDevVersion = Get-BumpedVersion -currentVersion $baseVersion -bumpType "patch"
    $nextState = [PSCustomObject]@{
        release_version = $releaseVersion
        dev_version = $nextDevVersion
        updated_utc = (Get-Date).ToUniversalTime().ToString("o")
    }
    $nextState | ConvertTo-Json -Depth 5 | Set-Content -Path $devStatePath -Encoding UTF8

    return $nextDevVersion
}

function Get-Author($manifest) {
    if ($manifest.author_name -and -not [string]::IsNullOrWhiteSpace($manifest.author_name)) {
        return $manifest.author_name
    }

    if ($manifest.author -and -not [string]::IsNullOrWhiteSpace($manifest.author)) {
        return $manifest.author
    }

    return "fierrof"
}

function Invoke-PackageBuild($manifest, [string]$channel) {
    $releaseVersion = $manifest.version_number
    [void](Get-SemVerParts -value $releaseVersion)

    $packageVersion = if ($channel -eq "dev") {
        Get-NextDevVersion -releaseVersion $releaseVersion
    } else {
        $releaseVersion
    }

    $modName = $manifest.name
    $author = Get-Author -manifest $manifest
    $zipName = "$author-$modName-$packageVersion.zip"
    $channelBinDir = Join-Path $binDir $channel

    Write-Host "Build channel: $channel"
    Write-Host "Release version: $releaseVersion"
    Write-Host "Package version: $packageVersion"

    Write-Host "Cleaning bin and obj folders..."
    foreach ($cleanDir in @("$solutionDir\ActionUI\bin", "$solutionDir\ActionUI\obj", "$projectDir\bin", "$projectDir\obj")) {
        if (Test-Path $cleanDir) {
            Remove-Item $cleanDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Host "Building and publishing project..."
    dotnet publish $projectFile -c Debug -o "$publishDir"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed for ActionUI.Plugin"
    }

    Write-Host "Copying assets..."
    if (-not (Test-Path $publishDir)) {
        New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
    }

    $buildManifest = [PSCustomObject]@{
        name = $manifest.name
        author = $(if ($manifest.author) { $manifest.author } else { $author })
        version_number = $packageVersion
        website_url = $manifest.website_url
        description = $manifest.description
        dependencies = @($manifest.dependencies)
    }
    $buildManifest | ConvertTo-Json -Depth 10 | Set-Content -Path "$publishDir\manifest.json" -Encoding UTF8

    Copy-Item "$solutionDir\README.md" -Destination $publishDir -Force
    Copy-Item "$solutionDir\CHANGELOG.md" -Destination $publishDir -Force

    $iconSource = "$solutionDir\icon.png"
    if (Test-Path $iconSource) {
        Copy-Item $iconSource -Destination $publishDir -Force
    } else {
        Write-Warning "icon.png not found at $iconSource"
    }

    $hiddenSlotImageSource = "$solutionDir\ActionSlotHiddenImage.png"
    if (-not (Test-Path $hiddenSlotImageSource)) {
        $hiddenSlotImageSource = "$solutionDir\WikiReadmeAssets\ActionSlotHiddenImage.png"
    }
    if (Test-Path $hiddenSlotImageSource) {
        Copy-Item $hiddenSlotImageSource -Destination "$publishDir\ActionSlotHiddenImage.png" -Force
        Write-Host "Copied hidden slot image: ActionSlotHiddenImage.png"
    } else {
        Write-Warning "ActionSlotHiddenImage.png not found at root or WikiReadmeAssets"
    }

    $dynamicSlotImageSource = "$solutionDir\ActionSlotDynamicImage.png"
    if (-not (Test-Path $dynamicSlotImageSource)) {
        $dynamicSlotImageSource = "$solutionDir\WikiReadmeAssets\ActionSlotDynamicImage.png"
    }
    if (Test-Path $dynamicSlotImageSource) {
        Copy-Item $dynamicSlotImageSource -Destination "$publishDir\ActionSlotDynamicImage.png" -Force
        Write-Host "Copied dynamic slot image: ActionSlotDynamicImage.png"
    } else {
        Write-Warning "ActionSlotDynamicImage.png not found at root or WikiReadmeAssets"
    }

    $assetBundleSource = "$solutionDir\Assets\asset-bundles\action-ui"
    if (Test-Path $assetBundleSource) {
        Copy-Item $assetBundleSource -Destination $publishDir -Force
        Write-Host "Copied asset bundle: action-ui"
    } else {
        Write-Error "Asset bundle not found at $assetBundleSource"
    }

    $profilesSource = "$solutionDir\Assets\Profiles"
    if (Test-Path $profilesSource) {
        Copy-Item $profilesSource -Destination "$publishDir\Profiles" -Recurse -Force
        Write-Host "Copied Profiles directory"
    } else {
        Write-Warning "Profiles directory not found at $profilesSource"
    }

    Write-Host "Verifying plugin DLLs..."
    $pluginDll = "$publishDir\ModifAmorphic.Outward.ActionUI.Plugin.dll"
    if (-not (Test-Path $pluginDll)) {
        Write-Host "Contents of publish dir:"
        Get-ChildItem $publishDir | Select-Object Name
        Write-Error "Main DLL not found at $pluginDll"
    }
    Write-Host "Found DLL at $pluginDll"

    $libDll = "$publishDir\ModifAmorphic.Outward.ActionUI.dll"
    if (Test-Path $libDll) {
        Write-Host "Found DLL at $libDll"
    } else {
        Write-Warning "Library DLL not found at $libDll"
    }

    Write-Host "Preparing output directory..."
    if (-not (Test-Path $channelBinDir)) {
        New-Item -ItemType Directory -Path $channelBinDir -Force | Out-Null
    }

    $zipPattern = "$author-$modName-*.zip"
    Get-ChildItem -Path $channelBinDir -Filter $zipPattern -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne $zipName } |
        Remove-Item -Force -ErrorAction SilentlyContinue

    Write-Host "Zipping to $zipName ..."
    $zipPath = "$channelBinDir\$zipName"
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
    Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath

    Write-Host "Build complete: $zipPath"
}

$manifest = Get-Manifest

switch ($Action) {
    "build" {
        Invoke-PackageBuild -manifest $manifest -channel $Channel
    }

    "set-version" {
        if ([string]::IsNullOrWhiteSpace($Version)) {
            Write-Error "-Version is required for -Action set-version"
        }

        [void](Get-SemVerParts -value $Version)
        $oldVersion = $manifest.version_number
        $manifest.version_number = $Version
        Save-Manifest -manifest $manifest
        Write-Host "Manifest version updated: $oldVersion -> $Version"

        if ($BuildAfterChange) {
            Invoke-PackageBuild -manifest $manifest -channel $Channel
        }
    }

    "bump" {
        $oldVersion = $manifest.version_number
        $newVersion = Get-BumpedVersion -currentVersion $oldVersion -bumpType $Bump
        $manifest.version_number = $newVersion
        Save-Manifest -manifest $manifest
        Write-Host "Manifest version bumped ($Bump): $oldVersion -> $newVersion"

        if ($BuildAfterChange) {
            Invoke-PackageBuild -manifest $manifest -channel $Channel
        }
    }
}
