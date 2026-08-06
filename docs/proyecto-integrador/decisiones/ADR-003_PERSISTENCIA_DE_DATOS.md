# ADR-003: Persistencia de datos

Fecha: 12 de junio de 2026  
Versión relacionada: 0.4  
Estado: Aceptada

## Contexto

El diseño requería guardar productos, proveedores, movimientos, lotes, conteos, pedidos y recepciones.

## Problema

Debíamos elegir entre archivos simples, base local o servidor remoto.

## Opciones

- Archivos JSON/CSV.
- SQLite local.
- Base remota.

## Decisión

Usar SQLite local.

## Razón

SQLite nos dio persistencia local, transacciones y un esquema relacional sin depender de servidor. En el repositorio aparece `sqlite-net-pcl`.

## Consecuencias

El sistema puede trabajar localmente. La parte pendiente es respaldo y sincronización si se usa en varios equipos.

## Riesgos

Pérdida de datos si el usuario elimina almacenamiento local y no hay respaldo.

## Evidencia

`InventoryDatabase.cs`, `DatabaseMigrator.cs`, `DataBaseInitialize.cs`, `InventorySystem.csproj`.

