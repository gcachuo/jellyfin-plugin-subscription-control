# EasyMovie Plugin Integration Tests

Pruebas de integración para el plugin EasyMovie Subscription Control de Jellyfin.

## Estructura

```
EasyMovie.Plugin.IntegrationTests/
├── Api/
│   └── SubscriptionClientIntegrationTests.cs    # Tests con HTTP real (WireMock)
└── Workflows/
    └── LibraryAccessSyncWorkflowTests.cs        # Tests de flujos completos
```

## Ejecutar Pruebas

### Todas las pruebas de integración
```bash
dotnet test EasyMovie.Plugin.IntegrationTests
```

### Con salida detallada
```bash
dotnet test EasyMovie.Plugin.IntegrationTests --logger "console;verbosity=detailed"
```

### Ejecutar todas las pruebas (unitarias + integración)
```bash
dotnet test
```

## Cobertura de Pruebas

### SubscriptionClient Integration (6 tests)
- ✅ **IT-SC-001** API response parsing - Plan completo
- ✅ **IT-SC-002** API response parsing - Sin plan (null)
- ✅ **IT-SC-003** Suscripción expirada
- ✅ **IT-SC-004** Error del servidor (500)
- ✅ **IT-SC-005** Caching funcional
- ✅ **IT-SC-006** Test mode configuration

### Library Access Sync Workflows (6 tests)
- ✅ **IT-WF-001** Restringir de "todas" a carpetas específicas
- ✅ **IT-WF-002** Expandir de carpetas específicas a "todas"
- ✅ **IT-WF-003** Deshabilitar Live TV
- ✅ **IT-WF-004** Habilitar Live TV
- ✅ **IT-WF-005** Sincronización completa de plan básico
- ✅ **IT-WF-006** Manejo de GUIDs mixtos (válidos/inválidos)

## Formato de Documentación

Cada prueba incluye comentarios XML con formato Given-When-Then:

```csharp
/// <summary>
/// Test ID: IT-SC-001
/// Given: Mock API returns valid subscription with basic plan
/// When: GetStatusAsync is called
/// Then: Returns parsed subscription status with plan details
/// </summary>
[Fact]
public async Task GetStatusAsync_ValidApiResponse_ParsesCorrectly()
```

### Convención de IDs
- **IT-SC-XXX**: SubscriptionClient integration tests
- **IT-WF-XXX**: Workflow integration tests

## Tecnologías

- **xUnit** - Framework de pruebas
- **WireMock.Net** - Mock HTTP server para pruebas de API
- **FluentAssertions** - Assertions legibles
- **Moq** - Mocking de dependencias internas
- **Microsoft.Extensions.Caching.Memory** - Cache en memoria

## Diferencias con Pruebas Unitarias

| Aspecto | Unitarias | Integración |
|---------|-----------|-------------|
| **Alcance** | Componente aislado | Múltiples componentes |
| **HTTP** | Mocked (Moq) | Real (WireMock) |
| **Velocidad** | ~1-2 segundos | ~3-4 segundos |
| **Propósito** | Lógica de negocio | Flujos completos |

## Escenarios Probados

### API Integration
- ✅ Parsing correcto de respuestas JSON
- ✅ Manejo de planes null
- ✅ Estados de suscripción (active, expired, expiring)
- ✅ Fail-safe en errores HTTP
- ✅ Caching con múltiples llamadas
- ✅ Test mode y test users

### Workflow Integration
- ✅ Cambios de permisos de biblioteca
- ✅ Control de acceso a Live TV
- ✅ Sincronización completa de plan
- ✅ Validación de GUIDs
- ✅ Persistencia de cambios

## Agregar Nuevas Pruebas

### Test de API con WireMock
```csharp
[Fact]
public async Task MyTest()
{
    // Setup mock server response
    _mockServer
        .Given(Request.Create().WithPath("/api/endpoint"))
        .RespondWith(Response.Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithBody("{ \"data\": \"value\" }"));
    
    // Execute test
    var result = await _client.CallApi();
    
    // Verify
    result.Should().NotBeNull();
}
```

### Test de Workflow
```csharp
[Fact]
public async Task MyWorkflowTest()
{
    // Arrange - Setup mocks and data
    var userManagerMock = new Mock<IUserManager>();
    var service = new UserPolicyService(userManagerMock.Object, logger);
    
    // Act - Execute workflow
    var policy = await service.GetUserPolicyAsync(user);
    service.SetLiveTvAccess(policy, true);
    await service.UpdateUserPolicyAsync(user, policy);
    
    // Assert - Verify end state
    capturedPolicy.EnableLiveTvAccess.Should().BeTrue();
}
```

## CI/CD

Las pruebas de integración se ejecutan:
- Después de las pruebas unitarias
- En cada commit a main
- Antes de crear releases
- En pull requests

## Notas

- WireMock crea un servidor HTTP real en puerto aleatorio
- Tests son independientes y pueden ejecutarse en paralelo
- Cache se limpia entre tests para evitar interferencia
- Tiempo de ejecución: ~3 segundos
- 12 pruebas de integración en total
