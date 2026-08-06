# Investigación: .NET MAUI, C# y XAML

Fecha documentada: 7 de junio de 2026

Documento reconstruido a partir de las evidencias actuales del proyecto.

## Problema que teníamos

Buscábamos una tecnología que permitiera formularios, navegación, persistencia local y una base de código mantenible. Además, necesitábamos que el equipo pudiera trabajar con C#.

## Opciones revisadas

Revisamos WPF, una aplicación web y .NET MAUI. WPF era fuerte para Windows, pero no resolvía Android. La web facilitaba despliegue, pero agregaba servidor. MAUI quedaba en medio: aplicación local, XAML y posibilidad multiplataforma.

## Qué elegimos

Elegimos .NET MAUI con C# y XAML. La evidencia está en `InventorySystem.csproj`, `App.xaml`, `AppShell.xaml` y las páginas de `AppPages/`.

## Ventajas

- Compartir lógica e interfaz entre plataformas.
- Usar XAML para pantallas.
- Integrar servicios por inyección de dependencias.
- Mantener C# para dominio, infraestructura y pruebas.

## Desventajas

- La interfaz requiere pruebas por plataforma.
- Algunas pantallas quedaron con mucha lógica en code-behind.
- La cámara, si se integra después, requiere permisos y componentes adicionales.

## Evidencia actual

Encontramos MAUI, Shell, XAML, code-behind y recursos compartidos. No encontramos CommunityToolkit.Mvvm ni comandos MVVM aplicados de manera completa.

## Consecuencias

La decisión nos permitió avanzar rápido con formularios y navegación, pero dejó como mejora futura ordenar más lógica con ViewModels.

## Referencias consultadas

| Título | Organización | URL | Fecha de consulta |
|---|---|---|---|
| What is .NET MAUI? | Microsoft Learn | https://learn.microsoft.com/en-us/dotnet/maui/what-is-maui | 2026-08-05 |
| .NET MAUI Shell navigation | Microsoft Learn | https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/shell/navigation | 2026-08-05 |

