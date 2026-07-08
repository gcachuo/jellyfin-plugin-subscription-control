#!/bin/bash

# Script para empaquetar, commit, push y crear release en GitHub
# Uso: ./release.sh "Mensaje del changelog"

set -e

if [ -z "$1" ]; then
    echo "❌ Error: Debes proporcionar un mensaje de changelog"
    echo "Uso: ./release.sh \"Mensaje del changelog\""
    exit 1
fi

CHANGELOG="$1"

# Leer la versión actual del package.sh
VERSION=$(grep -oP 'VERSION="\K[^"]+' package.sh)

if [ -z "$VERSION" ]; then
    echo "❌ Error: No se pudo leer la versión de package.sh"
    exit 1
fi

echo "📦 Versión: $VERSION"
echo "📝 Changelog: $CHANGELOG"
echo ""

# Actualizar .csproj con la versión
echo "📝 Actualizando EasyMovie.Plugin.csproj..."
sed -i "s|<AssemblyVersion>.*</AssemblyVersion>|<AssemblyVersion>${VERSION}</AssemblyVersion>|" EasyMovie.Plugin/EasyMovie.Plugin.csproj
sed -i "s|<FileVersion>.*</FileVersion>|<FileVersion>${VERSION}</FileVersion>|" EasyMovie.Plugin/EasyMovie.Plugin.csproj
sed -i "s|<Version>.*</Version>|<Version>${VERSION}</Version>|" EasyMovie.Plugin/EasyMovie.Plugin.csproj

echo "✅ .csproj actualizado"

# Actualizar meta.json con el nuevo changelog
echo "📝 Actualizando meta.json..."
# Escapar caracteres especiales para JSON
CHANGELOG_JSON=$(echo "$CHANGELOG" | sed 's/\\/\\\\/g' | sed 's/"/\\"/g')
sed -i "s|\"changelog\": \".*\"|\"changelog\": \"🔄 Changes\\\\r\\\\n\\\\r\\\\n- ${CHANGELOG_JSON}\"|" EasyMovie.Plugin/meta.json

echo "✅ meta.json actualizado"

# Actualizar build.yaml con el nuevo changelog
echo "📝 Actualizando build.yaml..."
CHANGELOG_ENTRY="  ## ${VERSION%.*}\n  - ${CHANGELOG}\n  \n"

# Insertar nuevo changelog después de "changelog: |-"
sed -i "/^changelog: |-$/a\\${CHANGELOG_ENTRY}" build.yaml

echo "✅ build.yaml actualizado"
echo ""

# Compilar
echo "🔨 Compilando..."
export PATH="$HOME/.dotnet:$PATH"
dotnet build EasyMovie.Plugin/EasyMovie.Plugin.csproj -c Release

# Empaquetar
echo "📦 Empaquetando..."
./package.sh

# Obtener checksum
CHECKSUM=$(grep -oP 'MD5 Checksum: \K[A-F0-9]+' <<< "$(./package.sh 2>&1)" || md5sum "EasyMovie.Plugin-${VERSION}.zip" | awk '{print toupper($1)}')

echo "🔐 Checksum: $CHECKSUM"

# Commit y push
echo "📤 Commit y push..."
git add -A
git commit -m "Release v${VERSION} - ${CHANGELOG}"
git push

# Crear release en GitHub
echo "🚀 Creando release en GitHub..."
gh release create "v${VERSION%.*}" \
    "EasyMovie.Plugin-${VERSION}.zip" \
    --title "v${VERSION%.*} - ${CHANGELOG}" \
    --notes "## 🔄 Cambios

${CHANGELOG}

### 📥 Instalación
Descarga \`EasyMovie.Plugin-${VERSION}.zip\` y extrae a tu directorio de plugins de Jellyfin, o actualiza desde el catálogo de plugins.

### 🔐 Checksum
MD5: \`${CHECKSUM}\`"

# Actualizar manifest.json
echo "📝 Actualizando manifest.json..."

TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")
RELEASE_URL="https://github.com/gcachuo/jellyfin-plugin-subscription-control/releases/download/v${VERSION%.*}/EasyMovie.Plugin-${VERSION}.zip"

# Escapar el changelog para JSON
CHANGELOG_JSON=$(echo "$CHANGELOG" | sed 's/\\/\\\\/g' | sed 's/"/\\"/g')

# Crear archivo temporal con la nueva entrada
cat > /tmp/new_version.json <<EOF
      {
        "timestamp": "${TIMESTAMP}",
        "targetAbi": "10.11.6.0",
        "checksum": "${CHECKSUM}",
        "version": "${VERSION}",
        "changelog": "🔄 Changes\\r\\n\\r\\n- ${CHANGELOG_JSON}",
        "dependencies": [],
        "sourceUrl": "${RELEASE_URL}"
      },
EOF

# Insertar nueva versión al inicio del array de versions usando awk
awk '/"versions": \[/ {print; system("cat /tmp/new_version.json"); next}1' manifest.json > /tmp/manifest_new.json
mv /tmp/manifest_new.json manifest.json
rm -f /tmp/new_version.json

echo "✅ manifest.json actualizado"

# Commit y push manifest
echo "📤 Commit y push manifest.json..."
git add manifest.json
git commit -m "Update manifest for v${VERSION}"
git push

echo ""
echo "✅ Release v${VERSION%.*} completado exitosamente!"
