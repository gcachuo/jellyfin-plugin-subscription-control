# Regression Tests

Pruebas de regresión para verificar que las reglas de negocio críticas se mantienen.

## Tipos de Pruebas de Regresión

### 1. Unit Regression Tests (TrialUserRegressionTests.cs)
**Propósito**: Verificar lógica de negocio aislada con mocks

- ✅ Rápidas (~2 segundos)
- ✅ No requieren infraestructura externa
- ✅ Se ejecutan siempre en CI/CD
- ✅ Verifican lógica pura de restricciones

**Ejecutar**:
```bash
dotnet test --filter "FullyQualifiedName~TrialUserRegressionTests"
```

**Cobertura**:
- REG-001 a REG-006: Lógica de restricciones de usuario trial

### 2. End-to-End Regression Tests (TrialUserEndToEndRegressionTests.cs)
**Propósito**: Verificar sistema completo con API REAL de producción

- ⚠️ Requieren API real configurada
- ⚠️ Hacen llamadas HTTP reales
- ⚠️ Dependen de datos de producción
- ✅ Detectan bugs que solo aparecen con datos reales
- ✅ Verifican que el contrato de API no ha cambiado

**Ejecutar**:
```bash
# Opción 1: Usar script helper
./run-regression-tests.sh

# Opción 2: Manualmente
export EASYMOVIE_API_URL="https://easymovie.lat/api/subscription.php"
dotnet test --filter "FullyQualifiedName~EndToEndRegressionTests"
```

**Cobertura**:
- E2E-REG-001: Flujo completo con API real
- E2E-REG-002: Estructura de respuesta de API
- E2E-REG-003: Consistencia de respuestas
- E2E-REG-004: Fail-safe con timeouts
- E2E-REG-005: 🔴 **TestMode debe ser FALSE** (CRÍTICO - previene releases inseguros)
- E2E-REG-006: Validez de GUIDs de carpetas

## ¿Cuándo Ejecutar Cada Tipo?

### Unit Regression (siempre)
```bash
# Antes de cada commit
dotnet test --filter "FullyQualifiedName~TrialUserRegressionTests"

# En CI/CD (automático)
# Se ejecutan en cada push
```

### End-to-End Regression (periódicamente)
```bash
# Antes de releases importantes
./run-regression-tests.sh

# Después de cambios en la API
export EASYMOVIE_API_URL="https://easymovie.lat/api/subscription.php"
dotnet test --filter "FullyQualifiedName~EndToEndRegressionTests"

# Semanalmente (verificar producción)
# Configurar en cron job o scheduled task
```

## Comportamiento Sin API Configurada

Si la variable de entorno `EASYMOVIE_API_URL` **NO** está configurada:
- ✅ Los tests end-to-end se **saltan automáticamente**
- ✅ No fallan
- ✅ Permiten que CI/CD continúe
- ✅ Solo se ejecutan cuando se configura explícitamente

Esto es intencional para:
- No depender de API externa en CI/CD
- Permitir desarrollo offline
- Ejecutar solo cuando se necesita verificar producción

## Escenarios Críticos Verificados

### Trial User Restrictions
- ✅ Solo puede acceder a 2 carpetas específicas
- ✅ NO puede acceder a contenido premium
- ✅ NO tiene acceso a Live TV
- ✅ EnableAllFolders siempre es false
- ✅ Lista vacía de carpetas = sin acceso

### API Contract
- ✅ Estructura de respuesta JSON correcta
- ✅ Campos requeridos presentes
- ✅ GUIDs válidos en folder IDs
- ✅ Respuestas consistentes
- ✅ Fail-safe funciona correctamente
- ✅ 🔴 **TestMode desactivado en producción** (CRÍTICO)

## Ejemplo de Salida

### Tests Saltados (sin API configurada)
```
Test Run Successful.
Total tests: 5
     Passed: 5
     
(Los tests se ejecutan pero retornan inmediatamente)
```

### Tests con API Real
```
🔴 Running End-to-End Regression Tests with REAL API

API URL: https://easymovie.lat/api/subscription.php

⚠️  WARNING: These tests will make REAL calls to the production API

Continue with real API tests? (y/N): y

🧪 Running regression tests...

✅ All end-to-end regression tests passed!

This confirms:
  ✓ Real API is working correctly
  ✓ Trial user restrictions are enforced
  ✓ API contract is stable
  ✓ Data quality is good
```

## Troubleshooting

### Tests fallan con API real
**Posibles causas**:
1. API cambió su contrato (campos renombrados/eliminados)
2. Datos de producción inconsistentes
3. Usuario "trial" no existe en producción
4. Problemas de red/timeout

**Solución**:
1. Verificar logs de tests para ver error específico
2. Probar API manualmente con curl
3. Revisar cambios recientes en API
4. Ajustar tests si el cambio es intencional

### Tests se saltan siempre
**Causa**: Variable de entorno no configurada

**Solución**:
```bash
export EASYMOVIE_API_URL="https://easymovie.lat/api/subscription.php"
```

## Mejores Prácticas

1. **Ejecutar unit regression antes de cada commit**
   ```bash
   dotnet test --filter "FullyQualifiedName~TrialUserRegressionTests"
   ```

2. **Ejecutar end-to-end regression antes de releases**
   ```bash
   ./run-regression-tests.sh
   ```

3. **Monitorear producción semanalmente**
   - Configurar scheduled task
   - Alertar si fallan

4. **Actualizar tests cuando API cambia intencionalmente**
   - Documentar cambios
   - Actualizar assertions

5. **No commitear cambios que rompan regression tests**
   - Son reglas de negocio críticas
   - Requieren aprobación explícita
