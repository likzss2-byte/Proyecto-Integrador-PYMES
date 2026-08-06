
## Propósito

Reunimos en esta carpeta la documentación técnica, académica y evolutiva del proyecto Inventario PYMES. La organizamos para que pueda leerse como un proceso de trabajo progresivo: primero planteamos el problema, después definimos requisitos, investigamos tecnologías, diseñamos el sistema, construimos prototipos y cerramos con una revisión final.

Esta documentación reconstruye de manera cronológica el proceso de desarrollo de Inventario PYMES a partir del código, los archivos y las evidencias disponibles en el repositorio. Las fechas representan las etapas documentales del proyecto y no sustituyen las fechas reales del historial de Git.

## Estado general

Al cierre documental actualizado encontramos una aplicación .NET MAUI con persistencia local en SQLite, navegación Shell, servicios de inventario, repositorios, consulta externa a Open Food Facts, escaneo de códigos de barras por cámara en Android y Windows, y pruebas automatizadas con xUnit.

También dejamos marcadas las partes no comprobadas o incompletas: verificamos en Windows que el botón fuera visible, que abriera la pantalla interna y que iniciara la captura de una cámara, pero no decodificamos un código físico durante esa ejecución ni probamos una webcam USB, un dispositivo Android o un lector HID real. La administración completa de varios negocios quedó parcial y no encontramos un proyecto de pruebas automatizadas de interfaz.

## Resultados finales verificados

- `dotnet restore`: correcto.
- `dotnet build InventorySystem.csproj -t:Rebuild -f net10.0-android`: correcto, 2 advertencias XA0141 y 0 errores.
- `dotnet build InventorySystem.csproj -t:Rebuild -f net10.0-windows10.0.19041.0`: correcto, 0 advertencias y 0 errores.
- `dotnet test`: correcto, 68 pruebas aprobadas, 0 fallidas y 0 omitidas.
- Verificación de interfaz en Windows: el botón **Escanear con cámara** apareció visible y habilitado en Nuevo producto; al pulsarlo se abrió el escáner y la captura alcanzó el estado `CAMERA_PREVIEW_STARTED`.
- Verificación final realizada el 5 de agosto de 2026.

## Índice cronológico

### Auditoría y cronología

1. [00 Auditoría del repositorio](00_AUDITORIA_DEL_REPOSITORIO.md)
2. [01 Cronología general del proyecto](01_CRONOLOGIA_GENERAL_DEL_PROYECTO.md)

### Versiones reconstruidas

3. [2026-05-26 Versión 0.1 — Planteamiento inicial](versiones/2026-05-26_VERSION_0_1_PLANTEAMIENTO_INICIAL.md)
4. [2026-05-29 Versión 0.2 — Análisis y requisitos](versiones/2026-05-29_VERSION_0_2_ANALISIS_Y_REQUISITOS.md)
5. [2026-06-05 Versión 0.3 — Investigación tecnológica](versiones/2026-06-05_VERSION_0_3_INVESTIGACION_TECNOLOGICA.md)
6. [2026-06-11 Versión 0.4 — Diseño del sistema](versiones/2026-06-11_VERSION_0_4_DISENO_DEL_SISTEMA.md)
7. [2026-06-19 Versión 0.5 — Prototipo funcional](versiones/2026-06-19_VERSION_0_5_PROTOTIPO_FUNCIONAL.md)
8. [2026-06-29 Versión 0.6 — Códigos de barras, SKU y API](versiones/2026-06-29_VERSION_0_6_CODIGOS_DE_BARRAS_SKU_Y_API.md)
9. [2026-07-09 Versión 0.7 — Control de inventario](versiones/2026-07-09_VERSION_0_7_CONTROL_DE_INVENTARIO.md)
10. [2026-07-19 Versión 0.8 — Proveedores, pedidos y negocios](versiones/2026-07-19_VERSION_0_8_PROVEEDORES_PEDIDOS_Y_NEGOCIOS.md)
11. [2026-07-28 Versión 0.9 — Interfaz, responsividad y estabilidad](versiones/2026-07-28_VERSION_0_9_INTERFAZ_RESPONSIVIDAD_Y_ESTABILIDAD.md)
12. [2026-08-05 Versión 1.0 — Finalización del proyecto](versiones/2026-08-05_VERSION_1_0_FINALIZACION_DEL_PROYECTO.md)

### Investigación

13. [2026-06-05 Investigación tecnológica general](investigacion/2026-06-05_INVESTIGACION_TECNOLOGICA_GENERAL.md)
14. [2026-06-07 .NET MAUI, C# y XAML](investigacion/2026-06-07_DOTNET_MAUI_CSHARP_Y_XAML.md)
15. [2026-06-08 Persistencia y base de datos](investigacion/2026-06-08_PERSISTENCIA_Y_BASE_DE_DATOS.md)
16. [2026-06-29 Lectura de códigos de barras](investigacion/2026-06-29_LECTURA_DE_CODIGOS_DE_BARRAS.md)
17. [2026-07-01 API de búsqueda de productos](investigacion/2026-07-01_API_DE_BUSQUEDA_DE_PRODUCTOS.md)
18. [2026-07-09 Control transaccional de inventario](investigacion/2026-07-09_CONTROL_TRANSACCIONAL_DE_INVENTARIO.md)
19. [2026-07-19 Multiempresa, proveedores y pedidos](investigacion/2026-07-19_MULTIEMPRESA_PROVEEDORES_Y_PEDIDOS.md)
20. [2026-07-28 Diseño responsivo en .NET MAUI](investigacion/2026-07-28_DISENO_RESPONSIVO_EN_DOTNET_MAUI.md)
21. [2026-08-01 Tecnologías avanzadas implementadas](investigacion/2026-08-01_TECNOLOGIAS_AVANZADAS_IMPLEMENTADAS.md)

### Decisiones técnicas

22. [ADR-001 Tipo de aplicación](decisiones/ADR-001_TIPO_DE_APLICACION.md)
23. [ADR-002 C#, XAML y .NET MAUI](decisiones/ADR-002_CSHARP_XAML_Y_DOTNET_MAUI.md)
24. [ADR-003 Persistencia de datos](decisiones/ADR-003_PERSISTENCIA_DE_DATOS.md)
25. [ADR-004 Arquitectura y navegación](decisiones/ADR-004_ARQUITECTURA_Y_NAVEGACION.md)
26. [ADR-005 Lectura de códigos de barras](decisiones/ADR-005_LECTURA_DE_CODIGOS_DE_BARRAS.md)
27. [ADR-006 Consulta de productos por API](decisiones/ADR-006_CONSULTA_DE_PRODUCTOS_POR_API.md)
28. [ADR-007 Generación y unicidad de SKU](decisiones/ADR-007_GENERACION_Y_UNICIDAD_DE_SKU.md)
29. [ADR-008 Movimientos de inventario](decisiones/ADR-008_MOVIMIENTOS_DE_INVENTARIO.md)
30. [ADR-009 Soporte para varios negocios](decisiones/ADR-009_SOPORTE_PARA_VARIOS_NEGOCIOS.md)
31. [ADR-010 Estrategia de pruebas](decisiones/ADR-010_ESTRATEGIA_DE_PRUEBAS.md)

### Arquitectura, pruebas y manuales

32. [2026-06-11 Arquitectura propuesta](arquitectura/2026-06-11_ARQUITECTURA_PROPUESTA.md)
33. [2026-08-05 Arquitectura final](arquitectura/2026-08-05_ARQUITECTURA_FINAL.md)
34. [2026-08-05 Modelo de datos](arquitectura/2026-08-05_MODELO_DE_DATOS.md)
35. [2026-07-30 Plan de pruebas](pruebas/2026-07-30_PLAN_DE_PRUEBAS.md)
36. [2026-08-02 Pruebas funcionales](pruebas/2026-08-02_PRUEBAS_FUNCIONALES.md)
37. [2026-08-03 Pruebas de interfaz y responsividad](pruebas/2026-08-03_PRUEBAS_DE_INTERFAZ_Y_RESPONSIVIDAD.md)
38. [2026-08-04 Pruebas de regresión](pruebas/2026-08-04_PRUEBAS_DE_REGRESION.md)
39. [2026-08-05 Resultados finales de pruebas](pruebas/2026-08-05_RESULTADOS_FINALES_DE_PRUEBAS.md)
40. [2026-08-05 Manual técnico](manuales/2026-08-05_MANUAL_TECNICO.md)
41. [2026-08-05 Manual de usuario](manuales/2026-08-05_MANUAL_DE_USUARIO.md)

### Cierre

42. [2026-08-05 Matriz final de trazabilidad](2026-08-05_MATRIZ_FINAL_DE_TRAZABILIDAD.md)
43. [2026-08-05 Auditoría cronológica](2026-08-05_AUDITORIA_CRONOLOGICA.md)
44. [2026-08-05 Documento final del proyecto](2026-08-05_DOCUMENTO_FINAL_DEL_PROYECTO.md)

## Orden recomendado de lectura

Recomendamos leer primero la auditoría, después la cronología general, luego las versiones 0.1 a 1.0 y finalmente el documento final. Las investigaciones, ADR, arquitectura, pruebas y manuales funcionan como anexos de soporte.

## Documento recomendado para entrega

Para la entrega académica principal usamos [2026-08-05_DOCUMENTO_FINAL_DEL_PROYECTO.md](2026-08-05_DOCUMENTO_FINAL_DEL_PROYECTO.md). Los demás documentos respaldan decisiones, evidencias, pruebas y operación del sistema.
