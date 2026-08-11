# EasyMovie Plugin — Instrucciones para agentes

## Propósito y estructura

Este repositorio contiene un plugin de Jellyfin que consulta la API de suscripciones de EasyMovie, sincroniza las políticas de acceso de los usuarios y decide cómo reproducir prerolls.

- `EasyMovie.Plugin/`: plugin de producción para Jellyfin.
  - `Api/`: cliente de suscripciones y controladores HTTP.
  - `Configuration/`: configuración persistida y página de administración (`configPage.html`).
  - `Models/`: modelos de la respuesta de suscripciones.
  - `Playback/`: decisión de prerolls e interceptación de reproducción.
  - `Providers/`: integración con `IIntroProvider`.
  - `Services/`: sincronización de políticas y compatibilidad con Jellyfin.
  - `Tasks/`: tareas programadas de Jellyfin.
  - `Web/`: integración JavaScript con Jellyfin Web.
- `EasyMovie.Plugin.Tests/`: pruebas unitarias xUnit.
- `EasyMovie.Plugin.IntegrationTests/`: pruebas de integración xUnit, WireMock, flujos y regresiones.
- `docs/`: decisiones y planes técnicos. Consultar `docs/PREROLL-RELIABILITY-PLAN.md` antes de modificar prerolls.
- `build.yaml`, `manifest.json` y `EasyMovie.Plugin/meta.json`: metadatos de distribución.
- `package.sh`: crea el ZIP de distribución.
- `release.sh`: automatiza empaquetado, commits, push y publicación de GitHub.

La solución es `EasyMovie Plugin.sln` y contiene los tres proyectos anteriores.

## Entorno y comandos

- Ejecutar todos los comandos mediante WSL desde `/mnt/f/RiderProjects/EasyMovie Plugin`.
- El proyecto requiere .NET 9. Usar exclusivamente `/home/gcachuo/.dotnet/dotnet` (SDK 9.0.315); el `dotnet` de sistema en WSL es .NET 8 y no sirve para compilar este repositorio.
- Para evitar que los scripts usen el SDK equivocado, invocar el binario explícitamente en los comandos de desarrollo y validación.

```bash
# Restaurar y compilar la solución
wsl --cd "/mnt/f/RiderProjects/EasyMovie Plugin" /home/gcachuo/.dotnet/dotnet build "EasyMovie Plugin.sln"

# Ejecutar la suite completa
wsl --cd "/mnt/f/RiderProjects/EasyMovie Plugin" /home/gcachuo/.dotnet/dotnet test "EasyMovie Plugin.sln" --verbosity normal

# Ejecutar sólo un proyecto de pruebas
wsl --cd "/mnt/f/RiderProjects/EasyMovie Plugin" /home/gcachuo/.dotnet/dotnet test EasyMovie.Plugin.Tests/EasyMovie.Plugin.Tests.csproj --verbosity normal
wsl --cd "/mnt/f/RiderProjects/EasyMovie Plugin" /home/gcachuo/.dotnet/dotnet test EasyMovie.Plugin.IntegrationTests/EasyMovie.Plugin.IntegrationTests.csproj --verbosity normal
```

La suite completa verificada actualmente contiene 60 pruebas: 32 unitarias y 28 de integración. Ejecutarla después de cambios de comportamiento, dependencias, configuración o registro de servicios.

## Convenciones de implementación

- Objetivo: `net9.0`, `Nullable` e `ImplicitUsings` están habilitados en todos los proyectos. Mantener las APIs nuevas anulables de forma explícita y no introducir advertencias de compilación.
- Usar los tipos y extensiones de Jellyfin ya referenciados (`Jellyfin.Controller` y `Jellyfin.Model` 10.11.6); no agregar dependencias ni abstraer APIs de Jellyfin sin comprobar primero el patrón existente.
- Registrar servicios de plugin y tareas programadas en `PluginServiceRegistrator.cs`. Si se agrega un servicio que Jellyfin deba resolver, agregar también el registro correspondiente y una prueba apropiada.
- Si se agregan recursos web del plugin, incluirlos explícitamente como `EmbeddedResource` en `EasyMovie.Plugin.csproj`; `configPage.html` y `Web/overlay.js` siguen este patrón.
- Las pruebas usan xUnit y FluentAssertions; las pruebas unitarias usan Moq y las de integración usan WireMock cuando simulan HTTP. Imitar los fixtures y convenciones de los tests vecinos.
- No editar `bin/`, `obj/`, ZIPs de release ni archivos de IDE. Están generados o ignorados por Git.

## Reglas de dominio críticas

- Si la API de suscripciones falla o responde de forma inválida, preservar el comportamiento fail-safe: el usuario no debe perder acceso por un error transitorio.
- Los usuarios expirados siguen un flujo independiente: se bloquea el contenido solicitado y se entrega el video de expiración configurado.
- Para usuarios activos, próximos a expirar y de cortesía, no reemplazar la fuente ni la cola del reproductor principal. La estrategia es: overlay web compatible, preroll nativo sólo para clientes validados y, en clientes desconocidos o incompatibles, continuar sin intro.
- Evitar bucles y duplicados: un intro del plugin nunca recibe otro intro y una sesión no debe recibir prerolls repetidos.
- Al modificar políticas, preservar las restricciones de usuarios trial y validar acceso a bibliotecas y Live TV. Las regresiones de `TrialUserRegressionTests` cubren reglas críticas.

## Pruebas de regresión y sistemas externos

- Las pruebas E2E sólo hacen llamadas a una API real cuando se define `EASYMOVIE_API_URL`; sin esa variable, sus comprobaciones externas se omiten o retornan rápidamente.
- `run-regression-tests.sh` usa por defecto una URL de producción y requiere confirmación interactiva. No ejecutarlo sin aprobación expresa del usuario.
- Las pruebas `PlansConfigRegressionTests` leen `PLANS_JSON_PATH` cuando está definido. Para un release, exigen que `test_mode` sea `false` y `test_users` esté vacío.
- Nunca activar `test_mode` ni añadir usuarios de prueba en una configuración destinada a producción.

## Empaquetado y releases

- Para una nueva versión, cambiar únicamente `VERSION` en `package.sh`. `package.sh` sincroniza automáticamente `EasyMovie.Plugin.csproj`, `EasyMovie.Plugin/meta.json` y `build.yaml`; `release.sh` crea después la entrada correspondiente en `manifest.json`. Cambiar `TARGET_ABI` sólo cuando cambie la compatibilidad objetivo de Jellyfin.
- `package.sh` modifica metadatos de versión, borra ZIPs de release anteriores y crea un ZIP nuevo. No ejecutarlo sin confirmación explícita.
- `release.sh` tiene efectos remotos: ejecuta `package.sh`, hace commits, push y crea un release de GitHub. Nunca ejecutarlo sin confirmación explícita para cada release.
- Verificar que el ZIP contenga únicamente `EasyMovie.Plugin.dll`, `logo.png` y `meta.json`, y que el checksum MD5 de `manifest.json` coincida con el ZIP publicado.
- Consultar `RELEASE-CHECKLIST.md` antes de un release; sus conteos históricos de tests pueden estar desactualizados, por lo que la salida real de `dotnet test` es la fuente de verdad.

## Git

- Antes de editar, revisar `git status` y no mezclar cambios existentes del usuario con el trabajo solicitado.
- No hacer `push`, crear releases ni realizar operaciones destructivas sin autorización expresa.
- Ignorar en commits los cambios que sean exclusivamente de finales de línea (CRLF/LF) y no contengan cambios reales de contenido.
