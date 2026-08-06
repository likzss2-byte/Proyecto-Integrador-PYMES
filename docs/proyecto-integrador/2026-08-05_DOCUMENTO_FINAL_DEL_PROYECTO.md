# Documento final del proyecto Inventario PYMES

Fecha: 5 de agosto de 2026

## 1. Portada 
**Proyecto:** Inventario PYMES  
**Tipo de proyecto:** Desarrollo tecnológico, investigación aplicada e implementación de un sistema de información para PYMES  
**Institución:** Universidad Politecnica de Baja California
**Programa educativo:** Ingenieria en Tecnologias de la Informacion e Innovacion Digital 
**Integrantes:** Martinez Meza Carlos Armando, Cardoza Leyva Leonel Armando, Muñoz Mendez Ernesto, Delgadillo Sañudo Selene Alejandra, Martinez Muro Axel   
**Fecha:** 5 de agosto de 2026

## 2. Resumen

Desarrollamos Inventario PYMES como una aplicación .NET MAUI para apoyar el control local de inventario en una pequeña o mediana empresa. El sistema permite registrar productos, proveedores, entradas, salidas, ajustes, lotes, caducidades, pedidos, recepciones y conteos físicos. También incorpora captura por código de barras con entrada manual, lector HID, imagen y cámara, además de consulta externa mediante Open Food Facts.

El cierre técnico mostró compilación correcta para Android y Windows, además de `dotnet test` con 68 pruebas aprobadas. En Windows comprobamos que el botón de cámara apareciera en Nuevo producto, abriera el escáner interno e iniciara la captura. Aun así, dejamos documentadas las funciones parciales o no verificadas: multiempresa completa, autenticación, un proyecto de pruebas UI, decodificación física por cámara, ejecución Android en dispositivo y lectores HID reales.

## 3. Introducción

Durante el proyecto partimos de un problema común: muchas PYMES controlan inventario con registros manuales. Eso puede provocar duplicados, existencias incorrectas y falta de trazabilidad. Nuestra propuesta fue construir una aplicación local que organizara productos y movimientos sin depender desde el inicio de un servidor remoto.

## 4. Antecedentes

Antes de iniciar el desarrollo documental, tomamos como antecedente el uso de hojas, libretas o archivos separados para controlar productos. El repositorio final muestra que el sistema evolucionó hacia una aplicación con dominio, infraestructura, base local y pruebas.

## 5. Planteamiento del problema

El problema fue la falta de control confiable sobre inventario. Sin validaciones y sin historial, es difícil saber qué producto existe, cuánto stock hay, por qué cambió y si el inventario físico coincide con el inventario teórico.

## 6. Justificación

Decidimos trabajar un sistema de inventario porque ayuda a reducir errores de captura, evita duplicados, registra movimientos y permite revisar diferencias. Para una PYME, la persistencia local también reduce dependencia de internet.

## 7. Objetivo general

Construir una aplicación de inventario para PYMES que permita registrar productos, proveedores y movimientos, controlar existencias, manejar lotes y caducidades, y comparar inventario físico contra inventario teórico.

## 8. Objetivos específicos

- Registrar productos con SKU y código de barras opcional.
- Evitar duplicados por SKU y código.
- Consultar información externa por código.
- Registrar proveedores.
- Controlar entradas, salidas y ajustes.
- Mantener historial de movimientos.
- Manejar lotes y caducidades.
- Registrar pedidos y recepciones.
- Realizar conteos físicos.
- Validar reglas mediante pruebas automatizadas.

## 9. Alcance

El alcance final incluye aplicación local .NET MAUI, SQLite, navegación Shell, formularios, servicios de inventario, repositorios, consulta externa y pruebas de reglas. No incluye backend remoto, roles, autenticación completa ni sincronización.

## 10. Limitaciones

- En Windows verificamos por automatización de interfaz la presencia del botón, la navegación y el inicio de captura de una cámara; no decodificamos un código físico ni distinguimos formalmente si el dispositivo iniciado era integrado o externo.
- No encontramos un proyecto de pruebas automatizadas de interfaz.
- Multiempresa quedó parcial.
- La validación de dígito verificador quedó integrada antes de consultar la API externa, pero no se probó desde una sesión manual de cámara.
- No verificamos ejecución real en Android.
- No probamos hardware lector físico real.

## 11. Tipo de proyecto

Clasificamos el trabajo como desarrollo tecnológico, investigación aplicada e implementación de un sistema de información para PYMES.

## 12. Metodología

Organizamos la reconstrucción con un ciclo incremental. No afirmamos Scrum ni Kanban porque no encontramos evidencia. Dividimos el trabajo en planteamiento, requisitos, investigación, diseño, prototipo, integración de códigos/API, control de inventario, proveedores/pedidos, interfaz y cierre.

## 13. Análisis de necesidades

Identificamos necesidades de registro confiable, centralización de productos, control de existencias, trazabilidad, reducción de duplicados, manejo de proveedores y comparación entre inventario físico y teórico.

## 14. Requisitos

Definimos requisitos como productos, códigos, SKU, API, proveedores, entradas, salidas, ajustes, existencias, conteos, faltantes, sobrantes, lotes, caducidades, pedidos, recepciones y diferentes negocios. La matriz final detalla su estado.

## 15. Investigación tecnológica

Comparamos aplicación web, escritorio y multiplataforma. La evidencia final confirma .NET MAUI, C# y XAML. Para datos elegimos SQLite local. Para códigos revisamos cámara, USB y Bluetooth; implementamos entrada HID/manual, lectura desde imagen y cámara en Android/Windows.

## 16. Diseño

Diseñamos una arquitectura con interfaz, servicios, dominio, repositorios y base local. También se propuso navegación con menú lateral, pantallas de productos, inventario, proveedores, entradas, salidas, pedidos y conteos.

## 17. Arquitectura

La arquitectura final es una aplicación local. `AppPages/` contiene pantallas, `src/InventorySystem.Domain/` contiene reglas y modelos, `src/InventorySystem.Infrastructure/` contiene servicios, repositorios y persistencia, y `tests/` contiene pruebas.

## 18. Modelo de datos

El modelo usa tablas como `businesses`, `products`, `suppliers`, `inventory_movements`, `inventory_lots`, `inventory_counts`, `purchase_orders` y `purchase_receipts`. Hay unicidad de SKU y código por negocio, llaves foráneas y movimientos inmutables.

## 19. Desarrollo

El desarrollo pasó de formularios básicos a reglas de inventario. Después conectamos códigos, API, movimientos, lotes, caducidades, pedidos, recepciones y conteos. Finalmente revisamos interfaz, menú y pruebas.

## 20. Tecnologías utilizadas

- .NET MAUI.
- C#.
- XAML.
- SQLite.
- `sqlite-net-pcl`.
- Shell/Flyout.
- Inyección de dependencias.
- `HttpClient`.
- JSON.
- Open Food Facts.
- ZXing.Net.
- SkiaSharp.
- xUnit.

## 21. Códigos de barras

El sistema permite escribir códigos y usar lectores HID porque estos escriben como teclado. También puede leer una imagen y escanear con cámara en Android y Windows. Android usa Camera2 con permiso `CAMERA`; Windows usa `MediaCapture` y selección de dispositivos de video dentro de la aplicación.

## 22. API

Usamos Open Food Facts para consultar productos por código. El endpoint está en `ExternalProductService.cs`. No encontramos claves ni límites documentados dentro del repositorio.

## 23. Control de inventario

Controlamos entradas, salidas y ajustes con servicios transaccionales. El stock actual se guarda en productos y cada cambio registra movimiento. Los movimientos se protegen contra edición y borrado.

## 24. Proveedores

El sistema registra proveedores y los relaciona con productos. También se usan proveedores en pedidos, lotes y filtros de conteo.

## 25. Pedidos

Los pedidos registran intención de compra. No aumentan inventario por sí mismos. Esto evita confundir pedido con entrada real.

## 26. Negocios

El modelo incluye `business_id`, lo que prepara separación por negocio. Sin embargo, no encontramos una interfaz completa para administrar varios negocios. Lo dejamos como parcial.

## 27. Pruebas

Encontramos pruebas automatizadas xUnit. Ejecutamos `dotnet test` el 5 de agosto de 2026 y obtuvimos 68 pruebas aprobadas. No encontramos pruebas automatizadas de interfaz.

Como comprobación adicional sobre el ejecutable Windows, usamos automatización de interfaz para abrir Nuevo producto, localizar el botón **Escanear con cámara**, pulsarlo y verificar que el servicio iniciara el lector de fotogramas. Esta comprobación no decodificó un código físico y no forma parte de las pruebas xUnit.

## 28. Resultados

- Restauración correcta.
- Compilación correcta.
- 0 advertencias en Windows y 2 advertencias XA0141 en Android por compatibilidad futura de SkiaSharp con páginas de 16 KB.
- 0 errores.
- 68 pruebas aprobadas.
- 0 pruebas fallidas.
- 0 pruebas omitidas.

## 29. Criterios de éxito

| Criterio | Resultado |
|---|---|
| Registro de productos | Verificado por código y pruebas. |
| Reducción de duplicados | Verificado para SKU y código. |
| Generación de SKU | Parcial. |
| Movimientos íntegros | Verificado. |
| Existencias correctas | Verificado por pruebas. |
| Faltantes y sobrantes | Verificado por pruebas. |
| API por código | Implementada. |
| Botón y arranque de cámara en Windows | Verificados; decodificación física pendiente. |
| Compilación | Correcta. |
| Pruebas | 68 aprobadas. |
| Responsividad | Parcial, sin prueba UI automatizada. |

## 30. Trabajo futuro

- Decodificación de un código físico con cámara integrada, webcam USB y Android físico.
- Administración completa de negocios.
- Usuarios, roles y autenticación.
- Pruebas de interfaz.
- Pruebas automatizadas de interfaz para validar checksum desde formularios.
- Respaldo/exportación de base local.
- Sincronización remota si se requiere.
- Reportes y exportación de inventario.

## 31. Conclusiones

Como equipo comprobamos que el proyecto no se limitó a formularios. La parte más importante fue entender que el inventario necesita reglas, transacciones, movimientos y pruebas. Resolvimos el control local de inventario en un nivel funcional importante, pero dejamos áreas claras para una siguiente versión.

Si volviéramos a iniciar, separaríamos antes la lógica de UI en ViewModels y definiríamos pruebas manuales de interfaz desde etapas tempranas. Aun así, el proyecto aporta una base real para administrar productos, movimientos, lotes, pedidos y conteos en una PYME.

## 32. Referencias

| Título | Organización | URL | Fecha de consulta |
|---|---|---|---|
| What is .NET MAUI? | Microsoft Learn | https://learn.microsoft.com/en-us/dotnet/maui/what-is-maui | 2026-08-05 |
| .NET MAUI Shell navigation | Microsoft Learn | https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/shell/navigation | 2026-08-05 |
| Consume a REST-based web service | Microsoft Learn | https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/rest | 2026-08-05 |
| File picker | Microsoft Learn | https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/file-picker | 2026-08-05 |
| Open Food Facts API documentation | Open Food Facts | https://openfoodfacts.github.io/openfoodfacts-server/api/ | 2026-08-05 |
| Check digit calculator | GS1 | https://www.gs1.org/services/check-digit-calculator | 2026-08-05 |
| SQLite Documentation | SQLite | https://www.sqlite.org/docs.html | 2026-08-05 |
| xUnit.net | xUnit.net | https://xunit.net/ | 2026-08-05 |

## 33. Anexos

- Auditoría del repositorio: `00_AUDITORIA_DEL_REPOSITORIO.md`.
- Cronología: `01_CRONOLOGIA_GENERAL_DEL_PROYECTO.md`.
- Versiones: carpeta `versiones/`.
- Investigaciones: carpeta `investigacion/`.
- Decisiones: carpeta `decisiones/`.
- Arquitectura: carpeta `arquitectura/`.
- Pruebas: carpeta `pruebas/`.
- Manuales: carpeta `manuales/`.
- Matriz final: `2026-08-05_MATRIZ_FINAL_DE_TRAZABILIDAD.md`.
- Auditoría cronológica: `2026-08-05_AUDITORIA_CRONOLOGICA.md`.
