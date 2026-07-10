# Release Checklist

Lista de verificación obligatoria antes de crear un release de producción.

## ⚠️ CRÍTICO: Tests de Seguridad

### 1. Verificar plans.json (OBLIGATORIO)

```bash
# Ejecutar desde el directorio del plugin
export PLANS_JSON_PATH="/ruta/a/api/plans.json"
dotnet test --filter "FullyQualifiedName~PlansConfigRegressionTests"
```

**Debe pasar**:
- ✅ CFG-REG-001: test_mode debe ser `false`
- ✅ CFG-REG-002: test_users debe estar vacío `[]`
- ✅ CFG-REG-003: Todos los planes tienen campos requeridos

**Si falla**:
```bash
# Editar plans.json
nano /ruta/a/api/plans.json

# Cambiar:
"test_mode": false,
"test_users": [],

# Volver a ejecutar tests
```

### 2. Verificar API de Producción (OBLIGATORIO)

```bash
# Solo si la API es accesible
export EASYMOVIE_API_URL="https://easymovie.lat/subscriptions/api/subscription.php"
dotnet test --filter "E2E_ProductionAPI_MustNotBeInTestMode"
```

**Debe pasar**:
- ✅ API debe ser accesible (no fail-safe)
- ✅ testMode debe ser `false` en respuesta JSON

**Si falla con "API not reachable"**:
- Verificar URL de API
- Verificar conectividad de red
- Verificar que el servidor esté corriendo

**Si falla con "testMode is true"**:
- Verificar `plans.json` en el servidor
- Reiniciar servidor PHP/Apache
- Limpiar cache de API

### 3. Ejecutar Todos los Tests (OBLIGATORIO)

```bash
# Ejecutar suite completa
dotnet test

# Debe mostrar:
# ✅ 28 unit tests passed
# ✅ 27 integration tests passed (24 + 3 config)
# ✅ 55 total tests passed
```

## 📦 Proceso de Release

### Paso 0: Actualizar Servidor (SI ES NECESARIO)

Si modificaste `plans.json`:

```bash
# 1. Verificar cambios locales
cd /mnt/f/PhpStormProjects/EasyMovie/api
git diff plans.json

# 2. Subir al servidor (elige tu método)

# Opción A: Git pull en servidor
ssh usuario@easymovie.lat
cd /ruta/a/api
git pull origin master
sudo systemctl restart apache2  # o php-fpm

# Opción B: SCP/rsync
scp plans.json usuario@easymovie.lat:/ruta/a/api/
ssh usuario@easymovie.lat "sudo systemctl restart apache2"

# 3. Verificar que se aplicó
curl -s "https://easymovie.lat/subscriptions/api/subscription.php?userId=test" | jq '.testMode'
# Debe retornar: false (no null)
```

### Paso 1: Preparación

```bash
# 1. Actualizar versión en VERSION variable de package.sh
nano package.sh
# Cambiar: VERSION="1.0.10.5"

# 2. Verificar que test_mode está desactivado LOCALMENTE
export PLANS_JSON_PATH="/mnt/f/PhpStormProjects/EasyMovie/api/plans.json"
dotnet test --filter "PlansConfigRegressionTests"

# 3. Verificar que test_mode está desactivado EN SERVIDOR
export EASYMOVIE_API_URL="https://easymovie.lat/subscriptions/api/subscription.php"
dotnet test --filter "E2E_ProductionAPI_MustNotBeInTestMode"
```

### Paso 2: Crear Package

```bash
# Ejecutar script de empaquetado
./package.sh

# El script automáticamente:
# 1. Actualiza versión en archivos
# 2. Compila en Release
# 3. Ejecuta 55 tests
# 4. Crea ZIP (solo si tests pasan)
# 5. Muestra checksum MD5
```

### Paso 3: Verificación Final

```bash
# Verificar que el ZIP fue creado
ls -lh EasyMovie.Plugin-*.zip

# Verificar contenido del ZIP
unzip -l EasyMovie.Plugin-*.zip
# Debe contener:
# - EasyMovie.Plugin.dll
# - logo.png
# - meta.json
```

### Paso 4: Crear GitHub Release

```bash
# Usar el comando sugerido por package.sh
gh release create v1.0.10.5 EasyMovie.Plugin-1.0.10.5.zip \
  --title "v1.0.10.5" \
  --notes "Release notes aquí"
```

### Paso 5: Actualizar Manifest

```bash
# Copiar el checksum MD5 del output de package.sh
# Editar manifest.json
nano manifest.json

# Actualizar:
# - version
# - checksum
# - sourceUrl (URL del release en GitHub)
# - timestamp

# Commit y push
git add manifest.json
git commit -m "Update manifest for v1.0.10.5"
git push
```

## 🔴 Checklist Pre-Release

Marcar cada item antes de crear el release:

- [ ] **CRÍTICO**: `test_mode: false` en plans.json
- [ ] **CRÍTICO**: `test_users: []` vacío en plans.json
- [ ] **CRÍTICO**: API de producción no está en test mode
- [ ] Todos los 55 tests pasan
- [ ] Versión actualizada en package.sh
- [ ] Package.sh ejecutado exitosamente
- [ ] ZIP creado y verificado
- [ ] Changelog/Release notes preparados
- [ ] GitHub release creado
- [ ] Manifest.json actualizado con checksum correcto
- [ ] Manifest.json commiteado y pusheado

## 🚨 Si Algo Falla

### Tests de configuración fallan
```bash
# Verificar plans.json
cat /ruta/a/api/plans.json | grep -A2 "test_mode"

# Debe mostrar:
# "test_mode": false,
# "test_users": [],
```

### Tests E2E fallan
```bash
# Verificar API manualmente
curl -s "https://easymovie.lat/subscriptions/api/subscription.php?userId=test" | jq '.testMode'

# Debe retornar: false

# Si retorna null, el archivo plans.json en el servidor no está actualizado
# Subir plans.json al servidor y reiniciar PHP/Apache
```

### Package.sh falla
```bash
# Ver logs detallados
./package.sh 2>&1 | tee package.log

# Revisar qué test falló
grep "Failed:" package.log
```

## 📝 Notas Importantes

1. **NUNCA** hacer release con `test_mode: true`
2. **SIEMPRE** ejecutar tests antes de crear package
3. **VERIFICAR** que el checksum MD5 es correcto en manifest
4. **PROBAR** el plugin en ambiente de staging antes de producción
5. **DOCUMENTAR** cambios en release notes

## 🔗 Referencias

- [Unit Tests README](EasyMovie.Plugin.Tests/README.md)
- [Integration Tests README](EasyMovie.Plugin.IntegrationTests/README.md)
- [Regression Tests README](EasyMovie.Plugin.IntegrationTests/Regression/README.md)
