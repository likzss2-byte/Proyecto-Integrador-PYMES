# ADR-008: Movimientos de inventario

Fecha: 10 de julio de 2026  
Versión relacionada: 0.7  
Estado: Aceptada

## Contexto

Las existencias no podían cambiar sin dejar rastro.

## Problema

Decidir si bastaba con actualizar stock actual o si debíamos conservar historial.

## Opciones

- Solo stock actual.
- Stock actual más movimientos.
- Stock calculado únicamente desde movimientos.

## Decisión

Guardar stock actual y registrar movimientos inmutables.

## Razón

Necesitábamos consultas rápidas y trazabilidad. El esquema contiene `inventory_movements` y triggers que bloquean actualización o eliminación.

## Consecuencias

Podemos auditar entradas, salidas y ajustes. A cambio, debemos mantener consistencia entre stock y movimientos.

## Riesgos

Que una operación futura modifique stock fuera de los servicios transaccionales.

## Evidencia

`InventoryTransactionService.cs`, `InventoryAdjustmentService.cs`, `DatabaseMigrator.cs`, pruebas de movimientos.

