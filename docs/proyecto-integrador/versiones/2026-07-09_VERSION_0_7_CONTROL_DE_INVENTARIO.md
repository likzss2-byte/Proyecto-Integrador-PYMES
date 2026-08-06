# Versión 0.7 — Control de inventario

Periodo de trabajo: 9 al 18 de julio de 2026  
Estado de la etapa: Incorporada con pruebas de reglas  
Versión anterior: 0.6  
Siguiente versión: 0.8

## Situación al iniciar

La versión anterior resolvía identificación de productos, pero todavía necesitábamos controlar cómo cambia el stock. En esta etapa incorporamos el modelo de entradas, salidas, ajustes, movimientos, conteos, lotes y caducidades.

## Entradas, salidas y ajustes

Encontramos servicios específicos para:

- Entradas y salidas (`InventoryTransactionService.cs`).
- Ajustes y conteos simples (`InventoryAdjustmentService.cs`).
- Persistencia transaccional (`InventoryDatabase.cs`).

Las entradas aumentan existencias, las salidas las disminuyen y los ajustes corrigen diferencias con motivo.

## Existencias teóricas e inventario físico

Separamos inventario teórico, que está registrado en el sistema, de inventario físico, que se captura durante conteos. Esta diferencia aparece en conteos y líneas de conteo (`InventoryCountSessionService.cs`).

## Movimientos e historial

Decidimos registrar movimientos para no perder trazabilidad. En el esquema final encontramos triggers que impiden modificar o borrar movimientos (`DatabaseMigrator.cs`).

## Lotes y caducidades

El proyecto maneja lotes, caducidades, alertas y consumo FEFO (`InventoryLotService.cs`, `InventoryLotPersistence.cs`). Esta parte fue necesaria para productos perecederos o controlados por fecha.

## Transacciones

Las escrituras se ejecutan con transacciones. Si una operación falla, se revierte. Esto aparece en `InventoryDatabase.WriteAsync`.

## Pruebas

En el estado final encontramos pruebas de entradas, salidas, cancelaciones, ajustes, transacciones, lotes, vencidos, FEFO y conteos. Las usamos como evidencia final, no como prueba histórica de esta fecha.

## Limitaciones

El control de inventario quedó fuerte en reglas, pero todavía faltaba relacionarlo con compras, proveedores y pedidos de forma completa.

