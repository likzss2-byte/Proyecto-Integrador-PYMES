# Manual técnico

Fecha: 5 de agosto de 2026

Documento reconstruido a partir de las evidencias actuales del proyecto.

## Requisitos

Para trabajar el proyecto necesitamos:

- SDK .NET 10.
- Workloads de .NET MAUI.
- Windows para ejecutar el target Windows revisado.
- Acceso a NuGet para restaurar dependencias.

## Instalación y restauración

Desde la raíz del repositorio:

```powershell
dotnet restore
```

Si faltan workloads:

```powershell
dotnet workload restore
```

## Compilación

```powershell
dotnet build InventorySystem.csproj -f net10.0-android
dotnet build InventorySystem.csproj -f net10.0-windows10.0.19041.0
```

Resultado verificado el 5 de agosto de 2026 mediante reconstrucción completa: Android y Windows compilaron con 0 errores. Windows produjo 0 advertencias; Android produjo 2 advertencias XA0141 de compatibilidad futura de SkiaSharp con páginas de 16 KB.

## Ejecución

La aplicación es .NET MAUI. En Windows puede ejecutarse desde Visual Studio o desde el binario generado bajo `bin/Debug/net10.0-windows10.0.19041.0/`.

## Estructura

| Ruta | Uso |
|---|---|
| `InventorySystem.slnx` | Solución. |
| `InventorySystem.csproj` | Aplicación MAUI. |
| `AppPages/` | Pantallas. |
| `src/InventorySystem.Domain/` | Entidades y reglas. |
| `src/InventorySystem.Infrastructure/` | Persistencia y servicios. |
| `tests/InventorySystem.Tests/` | Pruebas xUnit. |
| `Resources/` | Estilos, imágenes y recursos. |
| `Platforms/` | Configuración por plataforma. |

## Configuración

Los servicios y páginas se registran en `MauiProgram.cs`. No encontramos archivo externo de configuración para cambiar endpoint o timeout.

## Persistencia y base de datos

Usamos SQLite local. La base se llama `InventorySystem.db` y se crea en `FileSystem.AppDataDirectory`. La migración está en `DatabaseMigrator.cs`.

## API externa

El servicio real es Open Food Facts:

```text
https://world.openfoodfacts.org/api/v2/product/{barcode}.json
```

No encontramos claves ni autenticación en el repositorio.

## Permisos

Android declara internet, estado de red y permiso `CAMERA`. Windows declara capacidad `webcam` en `Platforms/Windows/Package.appxmanifest`.

La cámara se integra con una página compartida (`BarcodeScannerPage.xaml`), un control de vista previa (`BarcodeCameraPreview.cs`) y servicios por plataforma. Android usa Camera2; Windows usa `MediaCapture` y `MediaFrameReader`. La selección de cámara se habilita cuando el sistema reporta más de un dispositivo.

## Diagnóstico

| Problema | Revisión |
|---|---|
| No restaura | Revisar SDK, internet y fuentes NuGet. |
| No compila | Verificar workloads MAUI. |
| Error de SQLite | Revisar migraciones y base local. |
| API falla | Confirmar conexión; capturar producto manualmente. |
| Duplicado | Revisar SKU y código por negocio. |

## Pruebas

```powershell
dotnet test tests/InventorySystem.Tests/InventorySystem.Tests.csproj
```

Resultado verificado: 68 pruebas aprobadas, 0 fallidas y 0 omitidas.

## Agregar pantalla

1. Crear XAML y code-behind en `AppPages/`.
2. Registrar página en `MauiProgram.cs` si requiere inyección.
3. Registrar ruta en `AppShell.xaml.cs` si aplica.
4. Agregar navegación en `AppShell.xaml` si será parte del menú.
5. Mantener reglas fuera de la pantalla cuando sea posible.

## Agregar servicio

1. Crear clase en `src/InventorySystem.Infrastructure/Services/`.
2. Usar modelos del dominio.
3. Registrar servicio en `MauiProgram.cs`.
4. Agregar pruebas en `tests/InventorySystem.Tests/`.

## Agregar entidad

1. Definir modelo de dominio.
2. Agregar tabla en `DatabaseMigrator.cs`.
3. Incrementar versión de esquema.
4. Crear servicio o repositorio.
5. Probar migración y reglas.

## Extensión del sistema

Las extensiones más naturales son pruebas automatizadas de interfaz, validación física con distintos modelos de cámara, administración completa de negocios, usuarios, roles, exportación, respaldo y sincronización remota.
