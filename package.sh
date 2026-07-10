#!/bin/bash

VERSION="1.0.10.3"
BUILD_DIR="EasyMovie.Plugin/bin/Release/net9.0"
PACKAGE_NAME="EasyMovie.Plugin-${VERSION}.zip"

echo "📦 Packaging EasyMovie Plugin v${VERSION}..."

# Build first
echo "🔨 Building..."
dotnet build -c Release
if [ $? -ne 0 ]; then
    echo "❌ Build failed"
    exit 1
fi

# Verificar que existe el directorio de build
if [ ! -d "$BUILD_DIR" ]; then
    echo "❌ Build directory not found. Run 'dotnet build -c Release' first."
    exit 1
fi

# Crear el ZIP con los archivos necesarios (solo dll, logo y meta)
cd "${BUILD_DIR}" && zip "../../../../${PACKAGE_NAME}" EasyMovie.Plugin.dll logo.png meta.json && cd ../../../..

# Calcular checksum MD5
CHECKSUM=$(md5sum "${PACKAGE_NAME}" | awk '{print toupper($1)}')

echo ""
echo "✅ Package created: ${PACKAGE_NAME}"
echo "📊 MD5 Checksum: ${CHECKSUM}"
echo ""
echo "Next steps:"
echo "1. Create a GitHub release v${VERSION}"
echo "   gh release create v${VERSION} ${PACKAGE_NAME} --title \"v${VERSION}\" --notes \"Release notes\""
echo "2. Update manifest.json with checksum: ${CHECKSUM}"
echo "3. Commit and push manifest.json"
echo "   git add manifest.json && git commit -m \"Update manifest for v${VERSION}\" && git push"
