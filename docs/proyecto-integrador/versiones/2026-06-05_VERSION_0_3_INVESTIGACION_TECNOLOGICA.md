# Versión 0.3 — Investigación tecnológica

Periodo de trabajo: 5 al 10 de junio de 2026  
Estado de la etapa: Finalizada como investigación  
Versión anterior: 0.2  
Siguiente versión: 0.4


## Lo que recibimos

En la versión 0.2 ya teníamos requisitos, pero todavía no sabíamos si convenía construir una aplicación web, de escritorio o multiplataforma. También debíamos decidir cómo persistir los datos y cómo tratar códigos de barras.

## Opciones revisadas

Al principio consideramos continuar con una aplicación web. Tenía ventajas para centralizar datos, pero requería servidor y conexión estable. Después revisamos una aplicación de escritorio Windows, más simple para una PYME con lector físico. Finalmente evaluamos una aplicación multiplataforma para conservar la opción de Windows y Android.

## Decisiones reconstruidas

La evidencia final del repositorio muestra que terminamos usando:

- .NET MAUI (`InventorySystem.csproj`).
- C# y XAML (`AppPages/`, `src/`).
- SQLite local (`InventoryDatabase.cs`, `DatabaseMigrator.cs`).
- Shell para navegación (`AppShell.xaml`).
- Servicios y repositorios para separar responsabilidades (`src/InventorySystem.Infrastructure/`).
- Open Food Facts para consulta externa (`ExternalProductService.cs`).

En esta etapa lo tratamos como investigación, no como implementación terminada.

## Código de barras

Revisamos tres alternativas: cámara, lector USB y lector Bluetooth. La cámara ofrecía portabilidad, pero implicaba permisos y una vista de escaneo. Los lectores físicos en modo HID eran más simples: escriben el código como teclado y pueden enviar Enter. En ese momento todavía no presentábamos la cámara como terminada.

## Persistencia

Comparamos persistencia local contra servidor remoto. La evidencia final confirma SQLite local. Esta decisión respondía a la necesidad de operar sin servidor y conservar integridad transaccional.

## Pruebas

Definimos que las reglas críticas debían poder probarse de forma automatizada. En el cierre encontramos un proyecto xUnit con 68 pruebas, pero en esta etapa solo dejamos la estrategia planteada.

## Pendiente para la versión 0.4

Con la tecnología seleccionada como propuesta, necesitábamos diseñar arquitectura, pantallas, entidades y flujos antes de seguir desarrollando.
