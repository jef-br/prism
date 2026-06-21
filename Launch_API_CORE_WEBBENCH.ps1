# Build .NET solution first (covers API + WPF)
Write-Host "Building .NET solution..."
dotnet build jb/src/PRISM.sln
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }

# Ensure web deps are current
Write-Host "Installing web dependencies..."
Push-Location jb/src/workbench/web
npm install
if ($LASTEXITCODE -ne 0) { Write-Error "npm install failed"; exit 1 }
Pop-Location

# Launch all three in separate windows
Write-Host "Starting services..."
$root = $PSScriptRoot
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "Set-Location '$root'; dotnet run --project jb/src/api/Prism.Api.csproj"
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "Set-Location '$root/jb/src/workbench/web'; npm run dev"
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "Set-Location '$root'; dotnet run --project jb/src/workbench/wpf/Prism.Workbench.Wpf.csproj"

Write-Host "All services launched. Web workbench: http://localhost:3000"