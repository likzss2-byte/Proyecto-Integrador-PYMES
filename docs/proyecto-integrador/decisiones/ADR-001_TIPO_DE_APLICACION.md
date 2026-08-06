# ADR-001: Tipo de aplicación

Fecha: 27 de mayo de 2026  
Versión relacionada: 0.1  
Estado: Aceptada

## Contexto

Al inicio necesitábamos decidir si Inventario PYMES sería web, escritorio o multiplataforma. La decisión se reconstruye con base en el proyecto final.

## Problema

El sistema debía operar en una PYME, con posibilidad de usar Windows y lectores físicos, pero sin cerrar una futura ejecución en Android.

## Opciones

- Aplicación web.
- Aplicación de escritorio Windows.
- Aplicación multiplataforma.

## Decisión

Elegimos una aplicación multiplataforma.

## Razón

La evidencia final muestra .NET MAUI y targets para varias plataformas (`InventorySystem.csproj`). Esta opción nos permitió conservar operación local y preparar compatibilidad con Windows y Android.

## Consecuencias

Ganamos reutilización de código, pero asumimos la necesidad de probar por plataforma. En el cierre solo comprobamos compilación; no encontramos evidencia de ejecución real en Android.

## Riesgos

Que la aplicación compile en una plataforma pero necesite ajustes de interfaz o permisos en ejecución real.

## Evidencia

`InventorySystem.csproj`, `Platforms/`, `App.xaml`, `AppShell.xaml`.

