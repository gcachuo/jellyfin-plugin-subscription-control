#!/bin/bash

VERSION="1.0.0.0"
BUILD_DIR="EasyMovie.Plugin/bin/Release/net9.0"
PACKAGE_NAME="EasyMovie.Plugin-${VERSION}.zip"

echo "📦 Packaging EasyMovie Plugin v${VERSION}..."

# Verificar que existe el directorio de build
if [ ! -d "$BUILD_DIR" ]; then
    echo "❌ Build directory not found. Run 'dotnet build -c Release' first."
    exit 1
fi

# Crear el ZIP con los archivos necesarios
cd "$BUILD_DIR" || exit 1
zip -r "../../../../../${PACKAGE_NAME}" \
    EasyMovie.Plugin.dll \
    meta.json \
    logo.png \
    -x "*.pdb" "*.deps.json" "*.xml"

cd - > /dev/null

# Calcular checksum MD5
CHECKSUM=$(md5sum "$PACKAGE_NAME" | awk '{print toupper($1)}')

echo ""
echo "✅ Package created: ${PACKAGE_NAME}"
echo "📊 MD5 Checksum: ${CHECKSUM}"
echo ""
echo "Next steps:"
echo "1. Create a GitHub release v${VERSION}"
echo "2. Upload ${PACKAGE_NAME} to the release"
echo "3. Update manifest.json with checksum: ${CHECKSUM}"
echo "4. Commit and push manifest.json"
