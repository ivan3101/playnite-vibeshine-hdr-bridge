param(
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repoRoot "artifacts"

Push-Location $repoRoot
try {
    dotnet clean PlayniteHdrBridge.sln -c Release
    dotnet restore PlayniteHdrBridge.sln

    if (-not $SkipTests) {
        dotnet test tests/HdrBridge.Core.Tests/HdrBridge.Core.Tests.csproj -c Release --no-restore
        dotnet test src/SunshineLibrary.HdrBridge/Tests/SunshineLibrary.Tests.csproj -c Release --no-restore
    }

    dotnet build PlayniteHdrBridge.sln -c Release --no-restore

    if (Test-Path $artifacts) {
        Remove-Item $artifacts -Recurse -Force
    }
    New-Item -ItemType Directory -Path $artifacts | Out-Null

    function New-PlaynitePackage {
        param(
            [Parameter(Mandatory = $true)][string]$BuildDirectory,
            [Parameter(Mandatory = $true)][string]$PackageName
        )

        $stage = Join-Path $artifacts ("stage-" + $PackageName)
        New-Item -ItemType Directory -Path $stage | Out-Null
        Copy-Item (Join-Path $BuildDirectory "*") $stage -Recurse -Force
        Get-ChildItem $stage -Recurse -Include *.pdb,*.xml | Remove-Item -Force

        $zip = Join-Path $artifacts ($PackageName + ".zip")
        $pext = Join-Path $artifacts ($PackageName + ".pext")
        Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -CompressionLevel Optimal
        Move-Item $zip $pext
        Remove-Item $stage -Recurse -Force
    }

    New-PlaynitePackage `
        -BuildDirectory "src/HdrMetadataExporter/bin/Release/net462" `
        -PackageName "HdrCategorySync_v0.2.0"
    New-PlaynitePackage `
        -BuildDirectory "src/SunshineLibrary.HdrBridge/bin/Release/net462" `
        -PackageName "SunshineLibraryVibeshineHdrBridge_v0.2.0"

    Write-Host "Packages created in $artifacts"
}
finally {
    Pop-Location
}
