# ADR-010: Estrategia de pruebas

Fecha: 30 de julio de 2026  
Versión relacionada: 0.9  
Estado: Aceptada con cobertura parcial

## Contexto

El sistema ya tenía reglas críticas de inventario, pedidos, lotes y conteos.

## Problema

Necesitábamos comprobar reglas sin depender solo de pruebas manuales.

## Opciones

- Pruebas manuales.
- Pruebas automatizadas xUnit.
- Pruebas de interfaz.

## Decisión

Usar xUnit para reglas e infraestructura y dejar pruebas de interfaz como pendiente.

## Razón

Encontramos `InventorySystem.Tests` con 68 pruebas aprobadas por `dotnet test tests/InventorySystem.Tests/InventorySystem.Tests.csproj`.

## Consecuencias

Las reglas principales tienen respaldo automatizado. La navegación y responsividad no quedan cubiertas por esas pruebas.

## Riesgos

Que un cambio visual rompa flujos sin que `dotnet test` lo detecte.

## Evidencia

`tests/InventorySystem.Tests/InventoryLogicTests.cs`, resultado final de `dotnet test`.
