#!/bin/bash

VERSION="1.0.12.0"
TARGET_ABI="10.11.11.0"
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
sed -i "s/\"targetAbi\": \".*\"/\"targetAbi\": \"${TARGET_ABI}\"/" EasyMovie.Plugin/meta.json

# Update build.yaml
sed -i "s/version: '.*'/version: '${VERSION}'/" build.yaml
sed -i "s/targetAbi: '.*'/targetAbi: '${TARGET_ABI}'/" build.yaml

echo "✅ Version updated to ${VERSION}"

# Build
echo "🔨 Building..."
dotnet build -c Release
if [ $? -ne 0 ]; then
    echo "❌ Build failed"
    exit 1
fi

echo "✅ Build successful!"
echo ""

# Run tests
echo "🧪 Running tests..."
echo ""
echo "Running unit tests..."
dotnet test EasyMovie.Plugin.Tests --configuration Release --verbosity minimal --no-build
UNIT_TEST_RESULT=$?

echo ""
echo "Running integration tests..."
dotnet test EasyMovie.Plugin.IntegrationTests --configuration Release --verbosity minimal --no-build
INTEGRATION_TEST_RESULT=$?

if [ $UNIT_TEST_RESULT -ne 0 ] || [ $INTEGRATION_TEST_RESULT -ne 0 ]; then
    echo ""
    echo "❌ Tests failed! Package will not be created."
    echo "   Unit tests: $([ $UNIT_TEST_RESULT -eq 0 ] && echo '✅ Passed' || echo '❌ Failed')"
    echo "   Integration tests: $([ $INTEGRATION_TEST_RESULT -eq 0 ] && echo '✅ Passed' || echo '❌ Failed')"
    exit 1
fi

echo ""
echo "✅ All tests passed (52 tests total: 28 unit + 24 integration)!"
echo ""

# Verificar que existe el directorio de build
if [ ! -d "$BUILD_DIR" ]; then
    echo "❌ Build directory not found. Run 'dotnet build -c Release' first."
    exit 1
fi

# Limpiar ZIPs antiguos
echo "🧹 Cleaning old packages..."
OLD_ZIPS=$(find . -maxdepth 1 -name "EasyMovie.Plugin-*.zip" -type f)
if [ -n "$OLD_ZIPS" ]; then
    echo "$OLD_ZIPS" | while read -r zip; do
        echo "   Removing: $(basename "$zip")"
        rm -f "$zip"
    done
    echo "✅ Old packages removed"
else
    echo "   No old packages found"
fi
echo ""

# Crear el ZIP con los archivos necesarios (solo dll, logo y meta)
echo "📦 Creating package..."
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
