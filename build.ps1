Write-Host "Building BossNotifier (2-DLL solution)..." -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/2] Building BossNotifier.csproj..." -ForegroundColor Yellow
dotnet build BossNotifier.csproj -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build main project!" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host ""

Write-Host "[2/2] Building BossNotifier.Fika.csproj..." -ForegroundColor Yellow
dotnet build Fika\BossNotifier.Fika.csproj -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build Fika integration!" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host ""

Write-Host "[3/3] Rebuilding main project to package .bin file..." -ForegroundColor Yellow
dotnet build BossNotifier.csproj -c Release --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to rebuild main project!" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host ""

Write-Host "======================================" -ForegroundColor Green
Write-Host "Build complete!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host "Output: bin\Release\netstandard2.1\BossNotifier.zip"
Write-Host "  BepInEx/plugins/BossNotifier/"
Write-Host "    - BossNotifier.dll"
Write-Host "    - BossNotifier.FikaOptional.dll.bin"
Write-Host "======================================" -ForegroundColor Green
