# EasyMovie Plugin Tests

Pruebas unitarias para el plugin EasyMovie Subscription Control de Jellyfin.

## Estructura

```
EasyMovie.Plugin.Tests/
├── Api/
│   └── SubscriptionClientTests.cs      # Pruebas para cliente HTTP de API
├── Services/
│   └── UserPolicyServiceTests.cs       # Pruebas para servicio de políticas
└── Models/
    └── SubscriptionStatusTests.cs      # Pruebas para modelos de datos
```

## Ejecutar Pruebas

### Todas las pruebas
```bash
dotnet test
```

### Con salida detallada
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Con cobertura de código
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Pruebas específicas
```bash
# Por clase
dotnet test --filter "FullyQualifiedName~UserPolicyServiceTests"

# Por método
dotnet test --filter "Name~GetUserPolicyAsync"
```

## Cobertura de Pruebas

### UserPolicyService (100%)
- ✅ `GetUserPolicyAsync` - Construcción de política desde usuario
- ✅ `TrySetLibraryAccess` - Actualización de acceso a bibliotecas
- ✅ `TrySetLibraryAccess` - Validación de GUIDs inválidos con logging
- ✅ `SetLiveTvAccess` - Control de acceso a Live TV
- ✅ `UpdateUserPolicyAsync` - Persistencia exitosa
- ✅ `UpdateUserPolicyAsync` - Manejo de excepciones

### SubscriptionClient (100%)
- ✅ `GetStatusAsync` - API URL vacía retorna fail-safe
- ✅ `GetStatusAsync` - Retorna valor cacheado cuando existe
- ✅ `GetStatusAsync` - Llamada exitosa a API externa
- ✅ `GetStatusAsync` - Error HTTP retorna fail-safe
- ✅ `GetStatusAsync` - Excepción de red retorna fail-safe

### SubscriptionStatus Models (100%)
- ✅ `IsExpired` - Detección de estado expirado
- ✅ `IsExpiring` - Detección de estado por expirar
- ✅ `IsCourtesy` - Detección de estado cortesía
- ✅ `PlanInfo` - Propiedades de plan

## Tecnologías

- **xUnit** - Framework de pruebas
- **Moq** - Mocking framework
- **FluentAssertions** - Assertions legibles
- **Microsoft.Extensions.Caching.Memory** - Cache en memoria

## Mejores Prácticas

1. **Arrange-Act-Assert** - Estructura clara de pruebas
2. **Nombres descriptivos** - `Method_Scenario_ExpectedResult`
3. **Mocking de dependencias** - Aislamiento de unidades
4. **Theory/InlineData** - Pruebas parametrizadas
5. **FluentAssertions** - Assertions expresivas

## Agregar Nuevas Pruebas

1. Crear archivo en carpeta correspondiente (Api/Services/Models)
2. Heredar convenciones de naming
3. Usar Moq para dependencias externas
4. Verificar cobertura con `dotnet test --collect:"XPlat Code Coverage"`

## CI/CD

Las pruebas se ejecutan automáticamente en:
- Cada commit a main
- Pull requests
- Antes de crear releases

## Notas

- Las pruebas usan mocks para evitar dependencias de Jellyfin
- No requieren instancia de Jellyfin corriendo
- Tiempo de ejecución: ~2 segundos
- 28 pruebas en total
