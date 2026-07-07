$VERSION = "1.0.0.0"
$BUILD_DIR = "EasyMovie.Plugin/bin/Release/net9.0"
$PACKAGE_NAME = "EasyMovie.Plugin-$VERSION.zip"

Write-Host "📦 Packaging EasyMovie Plugin v$VERSION..." -ForegroundColor Cyan

# Verificar que existe el directorio de build
if (-not (Test-Path $BUILD_DIR)) {
    Write-Host "❌ Build directory not found. Run 'dotnet build -c Release' first." -ForegroundColor Red
    exit 1
}

# Archivos a incluir
$files = @(
    "$BUILD_DIR/EasyMovie.Plugin.dll",
    "$BUILD_DIR/meta.json",
    "$BUILD_DIR/logo.png"
)

# Crear el ZIP
Compress-Archive -Path $files -DestinationPath $PACKAGE_NAME -Force

# Calcular checksum MD5
$md5 = Get-FileHash -Path $PACKAGE_NAME -Algorithm MD5
$CHECKSUM = $md5.Hash

Write-Host ""
Write-Host "✅ Package created: $PACKAGE_NAME" -ForegroundColor Green
Write-Host "📊 MD5 Checksum: $CHECKSUM" -ForegroundColor Yellow
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Create a GitHub release v$VERSION"
Write-Host "2. Upload $PACKAGE_NAME to the release"
Write-Host "3. Update manifest.json with checksum: $CHECKSUM"
Write-Host "4. Commit and push manifest.json"
