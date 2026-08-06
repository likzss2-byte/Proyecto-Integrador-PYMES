# Investigación tecnológica general

Periodo documentado: 5 al 10 de junio de 2026

Documento reconstruido a partir de las evidencias actuales del proyecto.

## Problema que teníamos

Necesitábamos elegir una forma de construir el sistema sin cerrar la puerta a Windows ni a Android. También queríamos que la aplicación pudiera operar de forma local porque una PYME no siempre tiene infraestructura de servidor.

## Opciones revisadas

| Opción | Ventajas | Desventajas |
|---|---|---|
| Aplicación web | Centralización y acceso desde navegador. | Requiere servidor, despliegue y conexión más estable. |
| Escritorio Windows | Adecuada para lector físico e inventario en mostrador. | Menor portabilidad. |
| Multiplataforma | Reutiliza código para varios destinos. | Exige más pruebas por plataforma. |

## Qué elegimos y por qué

La evidencia final muestra que elegimos .NET MAUI. Nos permitió usar C# y XAML, compilar para Windows y Android, y mantener una base local con SQLite.

## Qué dejamos como alternativa

Dejamos aplicación web y servidor remoto como alternativa futura si se requiere sincronización entre dispositivos o sucursales.

## Evidencia actual

- Proyecto MAUI (`InventorySystem.csproj`).
- Configuración por plataformas (`Platforms/`).
- Persistencia local (`src/InventorySystem.Infrastructure/Data/`).
- Navegación Shell (`AppShell.xaml`).

## Consecuencias

La decisión ayudó a avanzar con una aplicación local. La desventaja fue que necesitamos validar cada plataforma por separado; en la revisión final confirmamos compilación, pero no ejecución real en Android.

## Relación con la siguiente versión

Esta investigación alimentó el diseño de arquitectura de la versión 0.4.

