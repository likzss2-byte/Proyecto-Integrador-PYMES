# ADR-002: C#, XAML y .NET MAUI

Fecha: 7 de junio de 2026  
Versión relacionada: 0.3  
Estado: Aceptada

## Contexto

Durante la investigación tecnológica revisamos cómo construir formularios, navegación y lógica con una base común.

## Problema

Necesitábamos una tecnología que nos permitiera crear pantallas, manejar eventos, conectar servicios y compilar para más de un destino.

## Opciones

- WPF.
- Aplicación web.
- .NET MAUI con C# y XAML.

## Decisión

Usar .NET MAUI, C# y XAML.

## Razón

El repositorio final confirma esa ruta. Las pantallas están en XAML, la lógica en C# y los servicios se registran en `MauiProgram.cs`.

## Consecuencias

Avanzamos con rapidez en formularios y navegación. Como consecuencia negativa, varias pantallas conservan lógica en code-behind.

## Riesgos

Si el sistema crece, conviene mover más lógica a ViewModels y comandos para mejorar mantenimiento y pruebas.

## Evidencia

`InventorySystem.csproj`, `AppPages/*.xaml`, `AppPages/*.xaml.cs`, `MauiProgram.cs`.

