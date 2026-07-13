param(
    [Parameter(Mandatory)]
    [string]$ApiKey,
    [string]$Source = "https://api.nuget.org/v3/index.json"
)

$root = Split-Path $PSCommandPath -Parent
$packages = @(
    "SignalFlux.Core\bin\Release\SignalFlux.Core.0.2.0.nupkg"
    "SignalFlux.IO\bin\Release\SignalFlux.IO.0.2.0.nupkg"
    "SignalFlux.Storage\bin\Release\SignalFlux.Storage.0.2.0.nupkg"
    "SignalFlux.TimeSeries\bin\Release\SignalFlux.TimeSeries.0.2.0.nupkg"
    "SignalFlux.Generators\bin\Release\SignalFlux.Generators.0.2.0.nupkg"
)

foreach ($pkg in $packages) {
    $path = Join-Path $root $pkg
    if (-not (Test-Path $path)) {
        Write-Warning "Not found: $path"
        continue
    }
    Write-Host "Pushing $path..."
    dotnet nuget push $path --api-key $ApiKey --source $Source
    if ($LASTEXITCODE -eq 0) {
        Write-Host "OK: $pkg"
    } else {
        Write-Host "FAILED: $pkg"
    }
}
