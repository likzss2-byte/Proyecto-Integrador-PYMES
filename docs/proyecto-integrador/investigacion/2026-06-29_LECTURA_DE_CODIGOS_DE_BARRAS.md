# Investigación: lectura de códigos de barras

Periodo documentado: 29 de junio al 2 de julio de 2026

Documento reconstruido a partir de las evidencias actuales del proyecto.

## Problema que teníamos

El registro manual era útil, pero lento. Necesitábamos capturar códigos sin confundir tres cosas distintas: escribir un código, usar un lector físico que actúa como teclado y escanear con cámara.

## Alternativa A: cámara

La cámara ofrecía una experiencia cómoda en Android, pero dependía de permisos, enfoque, iluminación, calidad de cámara y una vista de escaneo. En Windows también podía funcionar, pero con más variación de hardware por cámaras integradas, webcams USB y controladores.

En la versión actual encontramos implementación real de cámara: Android usa Camera2, `TextureView` e `ImageReader`; Windows usa `DeviceInformation`, `MediaCapture` y `MediaFrameReader`. La vista no abre una aplicación externa, sino una pantalla interna de Inventario PYMES.

## Alternativa B: lector USB

El lector USB en modo teclado HID era la opción más simple. Escribe el código en el campo activo y puede mandar Enter como sufijo. Tiene buena velocidad y precisión para uso continuo. El riesgo es que el campo no tenga foco o que el lector repita lecturas.

## Alternativa C: lector Bluetooth

El lector Bluetooth conserva la idea HID y agrega portabilidad. Sus riesgos son batería, emparejamiento, reconexión e interrupciones.

## Matriz de decisión

Escala: 1 bajo, 5 alto.

| Criterio | Cámara | USB HID | Bluetooth HID |
|---|---:|---:|---:|
| Costo | 5 | 3 | 2 |
| Velocidad | 3 | 5 | 5 |
| Precisión | 3 | 5 | 5 |
| Facilidad de integración | 2 | 5 | 4 |
| Windows | 3 | 5 | 4 |
| Android | 4 | 3 | 4 |
| Portabilidad | 5 | 2 | 5 |
| Uso continuo | 3 | 5 | 5 |
| Mantenimiento | 2 | 5 | 4 |

## Qué existe realmente

Encontramos captura por campo de texto, compatibilidad con lector HID, imagen seleccionada con ZXing.Net y SkiaSharp, y escaneo en vivo con cámara (`BarcodeScannerService.cs`, `BarcodeScannerPage.xaml.cs`, `AndroidBarcodeCameraScannerService.cs`, `WindowsBarcodeCameraScannerService.cs`).

## Flujo recomendado para HID

1. El lector escribe el código.
2. Envía Enter.
3. Normalizamos.
4. Validamos.
5. Buscamos localmente.
6. Consultamos API si no existe.
7. Mostramos confirmación.
8. El usuario corrige.
9. Validamos duplicados.
10. Registramos.

## Consecuencias

Recomendamos lector HID como opción principal para operación intensiva y cámara como complemento disponible en Android y Windows. La cámara quedó conectada con búsqueda local y API. En la revisión final iniciamos una cámara desde el flujo Windows; sigue pendiente validar la decodificación con códigos físicos, dispositivos Android y modelos específicos de cámara integrada o USB.

## Referencias consultadas

| Título | Organización | URL | Fecha de consulta |
|---|---|---|---|
| File picker | Microsoft Learn | https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/file-picker | 2026-08-05 |
| ZXing.Net | ZXing.Net | https://github.com/micjahn/ZXing.Net | 2026-08-05 |
| Check digit calculator | GS1 | https://www.gs1.org/services/check-digit-calculator | 2026-08-05 |
