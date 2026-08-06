# ADR-007: Generación y unicidad de SKU

Fecha: 3 de julio de 2026  
Versión relacionada: 0.6  
Estado: Parcial

## Contexto

El SKU debía identificar productos aunque no tuvieran código de barras.

## Problema

Necesitábamos evitar SKU repetidos y definir si se generarían automáticamente.

## Opciones

- SKU manual obligatorio.
- SKU automático centralizado.
- SKU sugerido desde la pantalla cuando hay datos externos.

## Decisión

El SKU es obligatorio y único por negocio. La generación automática quedó parcial, aplicada desde la UI en un caso específico.

## Razón

`InventoryRules` exige SKU. `ProductRepository` y el esquema evitan duplicados. `NewItemPage.xaml.cs` genera `EXT-...` si se acepta una sugerencia externa y el SKU está vacío.

## Consecuencias

La unicidad está cubierta. La generación automática debe centralizarse para ser consistente.

## Riesgos

Que otras pantallas creen productos sin seguir la misma regla de generación.

## Evidencia

`InventoryModels.cs`, `ProductRepository.cs`, `DatabaseMigrator.cs`, `NewItemPage.xaml.cs`.

