# Tecnologías avanzadas implementadas

Periodo documentado: 1 al 4 de agosto de 2026

Documento reconstruido a partir de las evidencias actuales del proyecto.

## Qué buscamos confirmar

No quisimos quedarnos en decir que usamos C# y XAML. Revisamos qué conceptos de mayor nivel aparecen realmente en el proyecto.

## Implementadas

| Tecnología o concepto | Problema que resolvió | Dónde aparece | Limitación |
|---|---|---|---|
| .NET MAUI | Aplicación multiplataforma | `InventorySystem.csproj` | Falta prueba real por dispositivo. |
| Shell/Flyout | Navegación lateral | `AppShell.xaml` | Requiere pruebas UI. |
| Inyección de dependencias | Crear servicios y páginas | `MauiProgram.cs` | Algunas pantallas aún concentran lógica. |
| Servicios y repositorios | Separar reglas y datos | `src/InventorySystem.Infrastructure/` | Mantener límites claros. |
| SQLite | Persistencia local | `InventoryDatabase.cs` | No sincroniza por sí sola. |
| Migraciones manuales | Evolución del esquema | `DatabaseMigrator.cs` | Requiere cuidado al cambiar tablas. |
| Transacciones | Integridad de operaciones | `InventoryDatabase.cs` | Evitar escrituras fuera de servicios. |
| `async`/`await` | Operaciones no bloqueantes | Servicios y páginas | Cancelación UI parcial. |
| `HttpClient` y JSON | API externa | `ExternalProductService.cs` | Depende de internet. |
| ZXing.Net y SkiaSharp | Decodificación de códigos desde imagen y fotogramas | `BarcodeScannerService.cs` | La calidad depende de imagen, enfoque e iluminación. |
| Android Camera2 | Vista previa y captura de fotogramas en Android | `AndroidBarcodeCameraScannerService.cs`, `BarcodeCameraPreviewHandler.cs` | Falta validación manual en dispositivo físico. |
| Windows MediaCapture | Vista previa, selección y lectura de cámaras en Windows | `WindowsBarcodeCameraScannerService.cs`, `BarcodeCameraPreviewHandler.cs` | La enumeración depende de permisos y hardware del equipo. |
| Abstracciones por plataforma | Mantener una pantalla compartida y servicios específicos | `IBarcodeCameraScannerService.cs`, `BarcodeScannerCoordinator.cs`, `MauiProgram.cs` | Requiere pruebas por plataforma. |
| VisualStateManager | Responsividad | XAML y estilos | Sin pruebas automatizadas visuales. |
| xUnit | Pruebas automatizadas | `tests/InventorySystem.Tests/` | No cubre interfaz. |

## Parciales

- MVVM: encontramos `InventoryCountRowViewModel`, pero no una aplicación completa del patrón.
- Commands: predominan eventos en code-behind.
- Validación EAN/UPC/GTIN: el checksum está integrado antes de consultar la API externa. En Windows comprobamos el arranque de cámara desde la interfaz; falta decodificar un código físico y confirmar su retorno al formulario durante una ejecución documentada.
- Multiempresa: existe `business_id`, falta administración completa.
- Logging: solo vimos logging de depuración configurado, no registro persistente.

## Investigadas o propuestas

- CommunityToolkit.Mvvm.
- Pruebas de interfaz.
- Sincronización remota.
- Usuarios y roles.

## Consecuencias

Confirmamos una base técnica suficiente para el cierre, pero también identificamos áreas donde el proyecto puede mejorar sin inventar funciones que todavía no existen.

## Referencias consultadas

| Título | Organización | URL | Fecha de consulta |
|---|---|---|---|
| What is .NET MAUI? | Microsoft Learn | https://learn.microsoft.com/en-us/dotnet/maui/what-is-maui | 2026-08-05 |
| SQLite Documentation | SQLite | https://www.sqlite.org/docs.html | 2026-08-05 |
| xUnit.net | xUnit.net | https://xunit.net/ | 2026-08-05 |
