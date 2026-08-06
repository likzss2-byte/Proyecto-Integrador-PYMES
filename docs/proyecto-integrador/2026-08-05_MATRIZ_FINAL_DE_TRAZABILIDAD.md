# Matriz final de trazabilidad

Fecha: 5 de agosto de 2026

Usamos esta matriz para relacionar requisitos definidos desde la versión 0.2 con la evidencia final del repositorio. Las fechas de definición e incorporación son documentales; no sustituyen el historial Git.

| ID | Requisito | Fecha de definición | Versión definida | Fecha de incorporación | Versión incorporada | Estado final | Evidencia | Observaciones |
|---|---|---:|---|---:|---|---|---|---|
| RF-001 | Registrar productos manualmente | 2026-05-29 | 0.2 | 2026-06-19 | 0.5 | Implementado | `NewItemPage.xaml.cs`, `ProductRepository.cs` | SKU obligatorio y validaciones. |
| RF-002 | Registrar productos mediante código de barras | 2026-05-29 | 0.2 | 2026-06-29 | 0.6 | Implementado | `BarcodeScannerService.cs`, `BarcodeScannerPage.xaml.cs`, `NewItemPage.xaml.cs` | Manual/HID/imagen/cámara. En Windows verificamos botón, navegación e inicio de captura; quedó pendiente decodificar un código físico y probar Android/webcam USB. |
| RF-003 | Generar automáticamente SKU | 2026-05-29 | 0.2 | 2026-06-29 | 0.6 | Parcial | `NewItemPage.xaml.cs` | Solo al aceptar sugerencia externa con campo vacío. |
| RF-004 | Consultar API externa | 2026-05-29 | 0.2 | 2026-07-01 | 0.6 | Implementado | `ExternalProductService.cs` | Open Food Facts. |
| RF-005 | Evitar códigos duplicados | 2026-05-29 | 0.2 | 2026-06-29 | 0.6 | Implementado | `ProductRepository.cs`, `DatabaseMigrator.cs` | Unicidad por negocio. |
| RF-006 | Evitar SKU duplicados | 2026-05-29 | 0.2 | 2026-06-29 | 0.6 | Implementado | `ProductRepository.cs`, `DatabaseMigrator.cs` | Unicidad por negocio. |
| RF-007 | Registrar proveedores | 2026-05-29 | 0.2 | 2026-07-19 | 0.8 | Implementado | `SupplierRepository.cs`, `NewPurveyorPage.xaml.cs` | Incluye datos de contacto. |
| RF-008 | Controlar entradas | 2026-05-29 | 0.2 | 2026-07-09 | 0.7 | Implementado | `InventoryTransactionService.cs`, `NewOrderPage.xaml.cs` | Aumenta stock y registra movimiento. |
| RF-009 | Controlar salidas | 2026-05-29 | 0.2 | 2026-07-09 | 0.7 | Implementado | `InventoryTransactionService.cs`, `NewSalePage.xaml.cs` | Disminuye stock y usa lotes. |
| RF-010 | Registrar ajustes | 2026-05-29 | 0.2 | 2026-07-09 | 0.7 | Implementado | `InventoryAdjustmentService.cs`, `ItemFullPage.xaml.cs` | Requiere motivo. |
| RF-011 | Consultar existencias | 2026-05-29 | 0.2 | 2026-07-09 | 0.7 | Implementado | `InventoryPage.xaml.cs`, `DashboardService.cs` | Stock teórico en producto. |
| RF-012 | Comparar físico y teórico | 2026-05-29 | 0.2 | 2026-07-09 | 0.7 | Implementado | `InventoryCountSessionService.cs` | Conteos por sesión. |
| RF-013 | Mostrar faltantes | 2026-05-29 | 0.2 | 2026-07-09 | 0.7 | Implementado | `InventoryCountPage.xaml.cs`, pruebas | Diferencia negativa. |
| RF-014 | Mostrar sobrantes | 2026-05-29 | 0.2 | 2026-07-09 | 0.7 | Implementado | `InventoryCountPage.xaml.cs`, pruebas | Diferencia positiva. |
| RF-015 | Manejar lotes | 2026-05-29 | 0.2 | 2026-07-09 | 0.7 | Implementado | `InventoryLotService.cs` | Lotes con existencias. |
| RF-016 | Registrar caducidades | 2026-05-29 | 0.2 | 2026-07-09 | 0.7 | Implementado | `InventoryLotService.cs`, `InventoryModels.cs` | Según modo de caducidad. |
| RF-017 | Registrar pedidos | 2026-05-29 | 0.2 | 2026-07-19 | 0.8 | Implementado | `PurchaseOrderService.cs` | Pedido no modifica stock. |
| RF-018 | Registrar recepciones | 2026-05-29 | 0.2 | 2026-07-19 | 0.8 | Implementado | `PurchaseOrderService.cs` | Recepción genera entrada. |
| RF-019 | Permitir diferentes negocios | 2026-05-29 | 0.2 | 2026-07-19 | 0.8 | Parcial | `BusinessService.cs`, `DatabaseMigrator.cs` | Modelo sí; UI completa no. |
| RF-020 | Diseño responsivo | 2026-06-05 | 0.3 | 2026-07-28 | 0.9 | Parcial | `Styles.xaml`, `InventoryPage.xaml` | Sin pruebas UI automatizadas. |
| RF-021 | Menú lateral | 2026-06-11 | 0.4 | 2026-06-19 | 0.5 | Implementado | `AppShell.xaml` | Shell/Flyout. |
| RF-022 | Pruebas automatizadas | 2026-06-05 | 0.3 | 2026-07-30 | 0.9 | Implementado | `tests/InventorySystem.Tests/` | 68 pruebas aprobadas. |
| RNF-001 | Funcionamiento local | 2026-05-29 | 0.2 | 2026-06-08 | 0.3 | Implementado | `InventoryDatabase.cs` | SQLite local. |
| RNF-002 | Compatibilidad Windows | 2026-06-05 | 0.3 | 2026-06-19 | 0.5 | Implementado | `InventorySystem.csproj` | Compilación Windows correcta. |
| RNF-003 | Compatibilidad Android | 2026-06-05 | 0.3 | 2026-06-19 | 0.5 | Implementado con validación pendiente de dispositivo | `InventorySystem.csproj`, `AndroidManifest.xml`, `AndroidBarcodeCameraScannerService.cs` | Compila; no ejecución manual en dispositivo físico verificada. |
| RNF-004 | Seguridad básica | 2026-05-29 | 0.2 | No aplica | No aplica | No implementado | `LogIn.xaml`, `SignIn.xaml` | Pantallas no integradas como autenticación funcional. |
| RN-001 | No modificar stock sin movimiento | 2026-05-29 | 0.2 | 2026-07-09 | 0.7 | Implementado | `InventoryTransactionService.cs` | Transacciones y movimientos. |
| RN-002 | Pedido no equivale a entrada | 2026-05-29 | 0.2 | 2026-07-19 | 0.8 | Implementado | `PurchaseOrderService.cs` | Stock cambia al recibir. |
| RN-003 | Validar checksum EAN/UPC/GTIN | 2026-06-29 | 0.6 | 2026-06-29 | 0.6 | Implementado | `BarcodeRules.IsChecksumValid`, `ExternalProductService.cs`, `InventoryLogicTests.cs` | La API externa no se consulta si el checksum es inválido. |
