# Auditoría del repositorio

Fecha de revisión: 5 de agosto de 2026  
Repositorio: `Proyecto-Integrador-PYMES`  
Rama revisada: `integracion-logica-inventario`

Este documento lo usamos como punto de control antes de reorganizar la documentación. Revisamos el repositorio completo y registramos solo lo que pudimos comprobar en archivos, configuración, historial Git y resultados de comandos.

## Comandos ejecutados

| Comando | Resultado |
|---|---|
| `git status --short` | Existían cambios previos no confirmados; después de esta tarea aparecen archivos modificados y nuevos para escaneo por cámara, pruebas y documentación. |
| `git log --oneline --all --decorate` | Encontramos historial desde estructura inicial hasta integración de inventario, lotes, pedidos, pruebas y menú de inventario. |
| `dotnet restore` | Correcto; todos los proyectos estaban actualizados para restauración. |
| `dotnet build InventorySystem.csproj -t:Rebuild -f net10.0-windows10.0.19041.0` | Correcto; 0 advertencias y 0 errores. |
| `dotnet build InventorySystem.csproj -t:Rebuild -f net10.0-android` | Correcto; 2 advertencias XA0141 por bibliotecas nativas de SkiaSharp sin compatibilidad declarada con páginas de 16 KB de Android 16 y 0 errores. |
| `dotnet test` | Correcto; 68 pruebas aprobadas, 0 fallidas y 0 omitidas. |

## Estructura general

Encontramos una solución `.slnx`, no una solución `.sln` tradicional (`InventorySystem.slnx`). La estructura principal quedó así:

- Aplicación MAUI en la raíz (`InventorySystem.csproj`, `App.xaml`, `AppShell.xaml`, `MauiProgram.cs`).
- Pantallas en XAML y code-behind (`AppPages/`).
- Componentes visuales reutilizables (`VisualElementsTemplates/`).
- Recursos, imágenes y estilos (`Resources/`).
- Configuración por plataforma (`Platforms/`).
- Dominio en biblioteca separada (`src/InventorySystem.Domain/`).
- Infraestructura en biblioteca separada (`src/InventorySystem.Infrastructure/`).
- Pruebas automatizadas (`tests/InventorySystem.Tests/`).
- Modelos heredados o previos en `Objects/`.
- Carpeta de documentación creada en `docs/proyecto-integrador/`.

## Proyectos encontrados

| Proyecto | Tipo | Evidencia |
|---|---|---|
| `InventorySystem` | Aplicación .NET MAUI | `InventorySystem.csproj` |
| `InventorySystem.Domain` | Biblioteca de dominio | `src/InventorySystem.Domain/InventorySystem.Domain.csproj` |
| `InventorySystem.Infrastructure` | Biblioteca de infraestructura | `src/InventorySystem.Infrastructure/InventorySystem.Infrastructure.csproj` |
| `InventorySystem.Tests` | Proyecto de pruebas xUnit | `tests/InventorySystem.Tests/InventorySystem.Tests.csproj` |

## Plataformas configuradas

El proyecto principal está configurado como aplicación .NET MAUI. Encontramos targets para Android y, bajo condición de sistema operativo, iOS, Mac Catalyst y Windows (`InventorySystem.csproj`). En Windows se compila `net10.0-windows10.0.19041.0`.

Android declara permisos de internet, estado de red y cámara (`Platforms/Android/AndroidManifest.xml`). Windows declara capacidad de webcam en el manifiesto (`Platforms/Windows/Package.appxmanifest`).

## Arquitectura identificada

La arquitectura real no tiene un backend separado. Trabajamos con una aplicación local que usa:

- Interfaz MAUI con XAML y code-behind (`AppPages/*.xaml`, `AppPages/*.xaml.cs`).
- Navegación Shell/Flyout (`AppShell.xaml`, `AppShell.xaml.cs`).
- Servicios registrados con inyección de dependencias (`MauiProgram.cs`).
- Reglas y entidades en dominio (`src/InventorySystem.Domain/InventoryModels.cs`).
- Persistencia SQLite y migraciones (`InventoryDatabase.cs`, `DatabaseMigrator.cs`, `DataBaseInitialize.cs`).
- Repositorios para productos y proveedores (`ProductRepository.cs`, `SupplierRepository.cs`).
- Servicios para inventario, lotes, pedidos, conteos, dashboard, códigos y API externa (`src/InventorySystem.Infrastructure/Services/`).

## Tecnologías reales

| Tecnología | Estado | Evidencia |
|---|---|---|
| .NET MAUI | Implementada | `InventorySystem.csproj`, `MauiProgram.cs` |
| C# | Implementada | `src/`, `AppPages/*.xaml.cs` |
| XAML | Implementada | `App.xaml`, `AppShell.xaml`, `AppPages/*.xaml` |
| SQLite | Implementada | `sqlite-net-pcl`, `InventoryDatabase.cs`, `DatabaseMigrator.cs` |
| Shell y Flyout | Implementada | `AppShell.xaml`, `AppShell.xaml.cs` |
| Inyección de dependencias | Implementada | `MauiProgram.cs` |
| Open Food Facts | Implementada | `ExternalProductService.cs` |
| `HttpClient` | Implementada | `ExternalProductService.cs`, `MauiProgram.cs` |
| JSON | Implementada | `ExternalProductService.cs` |
| ZXing.Net | Implementada para imagen | `InventorySystem.csproj`, `BarcodeScannerService.cs` |
| SkiaSharp | Implementada para imagen | `InventorySystem.csproj`, `BarcodeScannerService.cs` |
| Android Camera2 | Implementada | `Platforms/Android/AndroidBarcodeCameraScannerService.cs` |
| Windows MediaCapture | Implementada | `Platforms/Windows/WindowsBarcodeCameraScannerService.cs` |
| xUnit | Implementada | `tests/InventorySystem.Tests/` |
| CommunityToolkit.Mvvm | No encontrada | No encontramos `ObservableProperty`, `RelayCommand` ni paquete MVVM. |
| Entity Framework Core | No encontrada | No encontramos `DbContext` ni migraciones EF. |
| Cámara en vivo | Implementada | `BarcodeScannerPage.xaml`, `IBarcodeCameraScannerService.cs`, `AndroidBarcodeCameraScannerService.cs`, `WindowsBarcodeCameraScannerService.cs`, `BarcodeCameraPreviewHandler.cs`, `AndroidManifest.xml`, `Package.appxmanifest`. |

## Módulos existentes

| Módulo | Estado | Evidencia |
|---|---|---|
| Inicio y dashboard | Implementado | `MainPage.xaml.cs`, `DashboardService.cs` |
| Productos | Implementado | `NewItemPage.xaml.cs`, `ProductRepository.cs` |
| Código de barras | Implementado con validación pendiente de prueba física | `BarcodeScannerService.cs`, `BarcodeReadGuard.cs`, `BarcodeScannerPage.xaml.cs`, `NewItemPage.xaml.cs`, `NewOrderPage.xaml.cs`, `NewSalePage.xaml.cs`, `InventoryCountPage.xaml.cs`, `PurchaseOrdersPage.xaml.cs`, `InventoryPage.xaml.cs` |
| Proveedores | Implementado | `NewPurveyorPage.xaml.cs`, `PurveyorFullPage.xaml.cs`, `SupplierRepository.cs` |
| Inventario | Implementado | `InventoryPage.xaml.cs`, `InventoryCatalogService.cs` |
| Entradas | Implementado | `NewOrderPage.xaml.cs`, `InventoryTransactionService.cs` |
| Salidas | Implementado | `NewSalePage.xaml.cs`, `InventoryTransactionService.cs` |
| Ajustes | Implementado | `ItemFullPage.xaml.cs`, `InventoryAdjustmentService.cs` |
| Conteo físico | Implementado | `InventoryCountPage.xaml.cs`, `InventoryCountSessionService.cs` |
| Lotes y caducidades | Implementado | `InventoryLotService.cs`, `InventoryLotPersistence.cs` |
| Pedidos y recepciones | Implementado | `PurchaseOrdersPage.xaml.cs`, `PurchaseOrderService.cs` |
| Negocios | Parcialmente implementado | `BusinessService.cs`, `businesses` en migración |
| Autenticación | No encontrada como flujo funcional | `LogIn.xaml`, `SignIn.xaml` existen, pero no vimos integración real. |

## Funcionalidades completas

Confirmamos como implementadas:

- Registro manual de productos con validaciones (`NewItemPage.xaml.cs`, `ProductRepository.cs`, `InventoryRules`).
- Unicidad de SKU y código por negocio (`ProductRepository.cs`, `DatabaseMigrator.cs`).
- Registro y consulta de proveedores (`SupplierRepository.cs`, `NewPurveyorPage.xaml.cs`).
- Entradas y salidas con movimientos (`InventoryTransactionService.cs`).
- Ajustes de inventario (`InventoryAdjustmentService.cs`, `ItemFullPage.xaml.cs`).
- Conteos físicos con diferencias (`InventoryCountSessionService.cs`, `InventoryCountPage.xaml.cs`).
- Lotes, caducidades y consumo FEFO (`InventoryLotService.cs`, `InventoryLotPersistence.cs`).
- Pedidos y recepciones con idempotencia (`PurchaseOrderService.cs`, `PurchaseOrdersPage.xaml.cs`).
- Consulta externa a Open Food Facts (`ExternalProductService.cs`, `ProductLookupService.cs`).
- Pruebas automatizadas de reglas e infraestructura (`tests/InventorySystem.Tests/InventoryLogicTests.cs`).

## Funcionalidades parciales

- Registro por código de barras: encontramos captura manual/HID, lectura desde imagen y escaneo con cámara conectado a registro de producto, entradas, salidas, conteos, búsqueda de inventario, pedidos y recepciones (`BarcodeScannerService.cs`, `BarcodeScannerPage.xaml.cs`, `NewItemPage.xaml.cs`, `NewOrderPage.xaml.cs`, `NewSalePage.xaml.cs`, `InventoryCountPage.xaml.cs`, `PurchaseOrdersPage.xaml.cs`, `InventoryPage.xaml.cs`).
- Generación automática de SKU: aparece cuando se acepta una sugerencia externa y el campo SKU está vacío, pero el dominio sigue exigiendo SKU obligatorio (`NewItemPage.xaml.cs`, `InventoryRules`).
- Validación EAN/UPC/GTIN: encontramos validación de longitud y dígito verificador. La consulta externa ya evita enviar códigos con checksum inválido (`BarcodeRules`, `ExternalProductService.cs`, `InventoryLogicTests.cs`).
- Multiempresa: el modelo usa `business_id`, pero no encontramos administración completa de negocios desde UI (`BusinessService.cs`, `DatabaseMigrator.cs`).
- Responsividad: vimos `VisualStateManager` y estilos, pero no pruebas automatizadas visuales (`Styles.xaml`, `InventoryPage.xaml`, `PurchaseOrdersPage.xaml`).

## Funciones no encontradas

- Autenticación funcional.
- Roles y permisos.
- Backend remoto.
- Sincronización entre dispositivos.
- Entity Framework Core.
- Migraciones EF.
- Pruebas automatizadas de interfaz.
- Logging persistente.
- Converters o Behaviors propios con lógica relevante.

## Base de datos y entidades

La base local es `InventorySystem.db`, creada en `FileSystem.AppDataDirectory` (`DataBaseInitialize.cs`). El esquema se crea y migra con `DatabaseMigrator.cs`.

Entidades reales principales:

- `businesses`
- `products`
- `suppliers`
- `product_suppliers`
- `inventory_documents`
- `inventory_document_lines`
- `inventory_movements`
- `inventory_lots`
- `inventory_movement_lots`
- `inventory_counts`
- `inventory_count_lines`
- `inventory_count_lot_lines`
- `purchase_orders`
- `purchase_order_lines`
- `purchase_receipts`
- `purchase_receipt_lines`
- `recent_product_queries`
- `legacy_imports`

## Pruebas encontradas

El proyecto de pruebas usa xUnit (`tests/InventorySystem.Tests/InventorySystem.Tests.csproj`). Encontramos 68 pruebas en `InventoryLogicTests.cs`. Cubren productos, duplicados, reglas EAN/UPC/GTIN, prevención de consulta externa con checksum inválido, transacciones, entradas, salidas, cancelaciones, ajustes, migraciones, movimientos inmutables, lotes, caducidades, FEFO, pedidos, recepciones, dashboard y conteos físicos.

## Historial Git revisado

El historial disponible muestra una evolución técnica con commits de:

- Estructura inicial.
- Secciones de documentación previa.
- Dominio y persistencia transaccional.
- Pruebas de reglas e inventario.
- Integración de servicios MAUI.
- Lotes y caducidad.
- Pedidos y recepciones.
- Sesiones transaccionales de inventario.
- Flujos Windows y menú de inventario.

No usamos el historial para inventar fechas de trabajo. Las fechas de esta documentación son etapas académicas reconstruidas.

## Limitaciones y riesgos

- No verificamos ejecución manual completa en Android con dispositivo físico.
- En Windows verificamos que el botón de cámara apareciera en Nuevo producto, que abriera el escáner interno y que iniciara la captura de una cámara. No decodificamos un código físico, no identificamos formalmente el tipo de esa cámara y no probamos webcam USB, Android físico ni lector HID real.
- No encontramos pruebas de interfaz.
- No encontramos autenticación funcional.
- La API externa depende de internet.
- La administración multiempresa está incompleta desde el punto de vista de usuario.
- Había cambios previos no confirmados en archivos de UI y estilos; durante la integración de cámara modificamos pantallas relacionadas y preservamos cambios ajenos no vinculados al flujo de escaneo.
