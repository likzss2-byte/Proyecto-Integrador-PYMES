# ADR-004: Arquitectura y navegación

Fecha: 15 de junio de 2026  
Versión relacionada: 0.4  
Estado: Aceptada

## Contexto

Al diseñar el sistema necesitábamos separar pantallas, lógica y datos. También requeríamos una navegación entendible para usuarios operativos.

## Problema

Si toda la lógica quedaba en las pantallas, el proyecto sería difícil de probar y mantener.

## Opciones

- Code-behind con acceso directo a datos.
- Separar dominio, infraestructura, servicios y UI.
- MVVM completo desde el inicio.

## Decisión

Separar dominio e infraestructura, usar servicios y repositorios, y navegar con Shell/Flyout.

## Razón

El repositorio final contiene `src/InventorySystem.Domain`, `src/InventorySystem.Infrastructure`, `AppShell.xaml` y servicios registrados en DI.

## Consecuencias

La separación mejoró pruebas de reglas. La interfaz quedó parcialmente acoplada a code-behind, por lo que una mejora futura sería MVVM más completo.

## Riesgos

Que nuevas pantallas vuelvan a concentrar demasiada lógica.

## Evidencia

`AppShell.xaml`, `AppShell.xaml.cs`, `MauiProgram.cs`, `src/`.

