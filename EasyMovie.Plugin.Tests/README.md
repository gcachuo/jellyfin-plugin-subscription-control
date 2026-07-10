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
- ✅ **UPS-001** `GetUserPolicyAsync` - Construcción de política desde usuario
- ✅ **UPS-002** `TrySetLibraryAccess` - Actualización de acceso a bibliotecas
- ✅ **UPS-003** `TrySetLibraryAccess` - Validación de GUIDs inválidos con logging
- ✅ **UPS-004** `SetLiveTvAccess` - Habilitar acceso a Live TV
- ✅ **UPS-005** `SetLiveTvAccess` - Deshabilitar acceso a Live TV
- ✅ **UPS-006** `UpdateUserPolicyAsync` - Persistencia exitosa
- ✅ **UPS-007** `UpdateUserPolicyAsync` - Manejo de excepciones

### SubscriptionClient (100%)
- ✅ **SC-001** `GetStatusAsync` - API URL vacía retorna fail-safe
- ✅ **SC-002** `GetStatusAsync` - Retorna valor cacheado cuando existe
- ✅ **SC-003** `GetStatusAsync` - Llamada exitosa a API externa
- ✅ **SC-004** `GetStatusAsync` - Error HTTP retorna fail-safe
- ✅ **SC-005** `GetStatusAsync` - Excepción de red retorna fail-safe

### SubscriptionStatus Models (100%)
- ✅ **SS-001** `IsExpired` - Detección de estado expirado (case-insensitive)
- ✅ **SS-002** `IsExpiring` - Detección de estado por expirar (case-insensitive)
- ✅ **SS-003** `IsCourtesy` - Detección de estado cortesía (case-insensitive)
- ✅ **SS-004** `PlanInfo` - Propiedades de plan

## Formato de Documentación

Cada prueba incluye comentarios XML con formato Given-When-Then:

```csharp
/// <summary>
/// Test ID: UPS-001
/// Given: A valid user with permissions
/// When: GetUserPolicyAsync is called
/// Then: Returns a UserPolicy with correct permissions
/// </summary>
[Fact]
public async Task GetUserPolicyAsync_ValidUser_ReturnsPolicy()
```

### Convención de IDs
- **UPS-XXX**: UserPolicyService tests
- **SC-XXX**: SubscriptionClient tests  
- **SS-XXX**: SubscriptionStatus model tests

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
