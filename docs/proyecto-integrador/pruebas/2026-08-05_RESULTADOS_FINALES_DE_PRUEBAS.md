# Resultados finales de pruebas

Fecha: 5 de agosto de 2026

Verificación final realizada el 5 de agosto de 2026.

## Restauración

| Campo | Resultado |
|---|---|
| Comando | `dotnet restore` |
| Resultado | Correcto |
| Observación | Todos los proyectos estaban actualizados para restauración. |

## Compilación

| Campo | Resultado |
|---|---|
| Comando Android | `dotnet build InventorySystem.csproj -t:Rebuild -f net10.0-android` |
| Resultado Android | Correcto |
| Advertencias Android | 2 advertencias XA0141 de SkiaSharp para el requisito futuro de páginas de 16 KB en Android 16 |
| Errores Android | 0 |
| Comando Windows | `dotnet build InventorySystem.csproj -t:Rebuild -f net10.0-windows10.0.19041.0` |
| Resultado Windows | Correcto |
| Advertencias Windows | 0 |
| Errores Windows | 0 |
| Proyectos compilados | Dominio, infraestructura y aplicación MAUI |

## Pruebas automatizadas

| Campo | Resultado |
|---|---|
| Comando | `dotnet test` |
| Proyecto | `tests/InventorySystem.Tests/InventorySystem.Tests.csproj` |
| Total | 68 |
| Aprobadas | 68 |
| Fallidas | 0 |
| Omitidas | 0 |
| Duración reportada | 3 segundos para `InventorySystem.Tests.dll` |

## Qué sí quedó comprobado

Comprobamos por pruebas automatizadas reglas de productos, duplicados, validación EAN/UPC/GTIN, prevención de consulta externa con checksum inválido, entradas, salidas, cancelaciones, ajustes, transacciones, migraciones, movimientos inmutables, lotes, caducidades, FEFO, pedidos, recepciones, dashboard y conteos físicos.

También realizamos una comprobación asistida con automatización de interfaz sobre el ejecutable Windows. Navegamos a Nuevo producto, confirmamos que **Escanear con cámara** estaba visible, habilitado y dentro de pantalla, pulsamos el botón y comprobamos que se abriera la vista interna del escáner. Después de corregir el cambio de formato incompatible con `MediaCaptureSharingMode.SharedReadOnly`, el servicio alcanzó el mensaje operativo “Coloca el código dentro del recuadro” (`CAMERA_PREVIEW_STARTED`). Esta comprobación no forma parte de las 68 pruebas xUnit.

## Qué no quedó comprobado

- Un proyecto repetible de pruebas automatizadas de interfaz; solo ejecutamos la comprobación asistida descrita arriba.
- Lector USB o Bluetooth real.
- Decodificación y retorno de un código físico desde la cámara; tampoco identificamos formalmente si la cámara iniciada era integrada o externa.
- Operación con una webcam USB o cámara externa específica.
- Ejecución real en Android.
- Pruebas de carga.
- Simulación formal de red desconectada.

## Conclusión de pruebas

Los resultados finales respaldan la lógica principal del sistema, la presencia y navegación del botón de cámara en Windows y el inicio del lector de fotogramas. No respaldan todavía una lectura física completa, la experiencia visual completa en todos los tamaños ni la operación en Android o con cada tipo de cámara.
