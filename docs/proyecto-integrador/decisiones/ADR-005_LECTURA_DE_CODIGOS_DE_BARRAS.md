# ADR-005: Lectura de códigos de barras

Fecha: 30 de junio de 2026  
Versión relacionada: 0.6  
Estado: Aceptada con validación física pendiente

## Contexto

Queríamos reducir captura manual usando códigos de barras.

## Problema

Teníamos que decidir entre cámara, lector físico o entrada manual.

## Opciones

- Cámara.
- Lector USB/Bluetooth HID.
- Escritura manual.
- Imagen seleccionada desde archivo.

## Decisión

Implementamos entrada manual/HID, lectura desde imagen y escaneo en vivo con cámara. La cámara se resolvió con una página compartida y servicios por plataforma: Android usa Camera2 y Windows usa `MediaCapture` sin abrir la aplicación Cámara externa.

## Razón

El lector HID se conserva porque es simple y compatible con formularios existentes. La cámara se agregó como complemento para Android y Windows, conectada al mismo flujo de códigos, búsqueda local, consulta externa, prevención de duplicados y SKU.

## Consecuencias

La operación con lector físico sigue siendo viable si el lector escribe en el campo activo. La experiencia de cámara en vivo permite seleccionar cámaras cuando el sistema reporta más de un dispositivo.

## Riesgos

Lecturas duplicadas, campo sin foco, permisos de cámara denegados, cámaras ocupadas por otra aplicación o falta de validación manual con hardware real.

## Evidencia

`BarcodeScannerService.cs`, `BarcodeReadGuard.cs`, `BarcodeScannerPage.xaml`, `BarcodeScannerPage.xaml.cs`, `IBarcodeCameraScannerService.cs`, `AndroidBarcodeCameraScannerService.cs`, `WindowsBarcodeCameraScannerService.cs`, `NewItemPage.xaml.cs`, `NewOrderPage.xaml.cs`, `NewSalePage.xaml.cs`, `InventoryCountPage.xaml.cs`, `PurchaseOrdersPage.xaml.cs`, `InventoryPage.xaml.cs`, `Platforms/Android/AndroidManifest.xml`, `Platforms/Windows/Package.appxmanifest`.
