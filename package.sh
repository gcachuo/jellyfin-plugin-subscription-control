#!/bin/bash

VERSION="1.0.10.4"
BUILD_DIR="EasyMovie.Plugin/bin/Release/net9.0"
PACKAGE_NAME="EasyMovie.Plugin-${VERSION}.zip"
TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%S.0000000Z")

echo "📦 Packaging EasyMovie Plugin v${VERSION}..."

# Update version in all files
echo "📝 Updating version in project files..."

# Update .csproj
sed -i "s/<AssemblyVersion>.*<\/AssemblyVersion>/<AssemblyVersion>${VERSION}<\/AssemblyVersion>/" EasyMovie.Plugin/EasyMovie.Plugin.csproj
sed -i "s/<FileVersion>.*<\/FileVersion>/<FileVersion>${VERSION}<\/FileVersion>/" EasyMovie.Plugin/EasyMovie.Plugin.csproj
sed -i "s/<Version>.*<\/Version>/<Version>${VERSION}<\/Version>/" EasyMovie.Plugin/EasyMovie.Plugin.csproj

# Update meta.json
sed -i "s/\"version\": \".*\"/\"version\": \"${VERSION}\"/" EasyMovie.Plugin/meta.json
sed -i "s/\"timestamp\": \".*\"/\"timestamp\": \"${TIMESTAMP}\"/" EasyMovie.Plugin/meta.json

# Update build.yaml
sed -i "s/version: '.*'/version: '${VERSION}'/" build.yaml

echo "✅ Version updated to ${VERSION}"

# Build
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
