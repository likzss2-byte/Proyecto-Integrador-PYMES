# Investigación: control transaccional de inventario

Periodo documentado: 9 al 13 de julio de 2026

Documento reconstruido a partir de las evidencias actuales del proyecto.

## Problema que teníamos

Una entrada o salida no podía actualizar solo un número. Necesitábamos que el cambio de stock y el movimiento histórico se guardaran juntos o no se guardaran.

## Opciones revisadas

- Actualizar solo `stock`.
- Calcular existencias únicamente desde movimientos.
- Guardar stock actual y movimientos históricos.

## Qué elegimos

Elegimos guardar stock actual en productos y registrar movimientos. La evidencia está en `products.stock_milli`, `inventory_movements`, `InventoryTransactionService.cs` y `InventoryDatabase.cs`.

## Por qué

El stock actual facilita consultas rápidas. Los movimientos permiten trazabilidad. Las transacciones reducen el riesgo de inconsistencias.

## Ventajas

- Entradas, salidas y ajustes quedan rastreables.
- Las pruebas pueden comprobar consistencia.
- Los movimientos quedan protegidos por triggers.

## Desventajas

- Hay más complejidad.
- Debemos evitar modificar stock fuera de los servicios.

## Evidencia actual

Encontramos transacciones, rollback, movimientos inmutables y pruebas de atomicidad en `InventoryLogicTests.cs`.

## Relación con la siguiente versión

Este control fue necesario para integrar proveedores, pedidos y recepciones sin confundir pedido con entrada real.

