# ADR-006: Consulta de productos por API

Fecha: 1 de julio de 2026  
Versión relacionada: 0.6  
Estado: Aceptada

## Contexto

El registro manual de productos podía ser lento y propenso a errores.

## Problema

Debíamos decidir si el sistema consultaría información externa por código.

## Opciones

- Solo catálogo local.
- Registro manual.
- Open Food Facts.
- Servicios comerciales.

## Decisión

Buscar primero localmente y después consultar Open Food Facts solo si el código tiene formato soportado y checksum EAN/UPC/GTIN válido.

## Razón

La evidencia final muestra `ProductLookupService` y `ExternalProductService` con endpoint de Open Food Facts. También muestra `BarcodeRules.IsChecksumValid` y pruebas que evitan consultar la API con checksum inválido.

## Consecuencias

Reducimos captura cuando hay datos externos. También dependemos de internet y de la calidad de la fuente.

## Riesgos

Producto no encontrado, respuesta incompleta, servicio externo no disponible o códigos no estándar que sí existan localmente pero no deban consultarse externamente.

## Evidencia

`ProductLookupService.cs`, `ExternalProductService.cs`, `BarcodeRules`, `InventoryLogicTests.cs`, `NewItemPage.xaml.cs`.
