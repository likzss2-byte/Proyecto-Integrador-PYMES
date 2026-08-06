# Versión 0.5 — Primer prototipo funcional

Periodo de trabajo: 19 al 28 de junio de 2026  
Estado de la etapa: Finalizada con pendientes  
Versión anterior: 0.4  
Siguiente versión: 0.6

## Punto de partida

Partimos del diseño de la versión 0.4 y comenzamos a armar una aplicación utilizable. La evidencia final del repositorio muestra una aplicación MAUI con solución, páginas XAML, recursos y configuración por plataforma.

## Trabajo realizado

En esta etapa ubicamos como razonable el inicio de:

- Creación de la solución y proyecto MAUI.
- Configuración inicial de plataformas.
- Pantallas básicas.
- Menú lateral.
- Formularios iniciales.
- Modelos de producto y proveedor.
- Persistencia local inicial.

No presentamos todavía como completos API, cámara, conteos, pedidos, recepciones ni multiempresa.

## Navegación inicial

El menú principal quedó como dirección de diseño. En el estado final lo encontramos implementado con Shell y Flyout (`AppShell.xaml`, `AppShell.xaml.cs`).

## Registro manual

El primer prototipo se centró en que el usuario pudiera capturar datos. La versión final tiene esa función en `NewItemPage.xaml.cs` y persistencia en `ProductRepository.cs`, pero en esta etapa lo documentamos como avance inicial.

## Validaciones iniciales

Detectamos que no bastaba con crear campos; era necesario impedir productos inválidos. Dejamos como siguiente trabajo reforzar duplicados, SKU y códigos.

## Errores encontrados

La primera propuesta no resolvía bien los duplicados ni la trazabilidad. También vimos que las pantallas podían crecer demasiado si la lógica quedaba concentrada en code-behind.

## Pruebas manuales iniciales

Para esta etapa planteamos pruebas manuales de navegación y captura. No registramos resultados históricos como aprobados porque no encontramos evidencia de ejecución formal en esa fecha.

## Pendiente para la versión 0.6

Todavía no se consultaba información externa al escribir un código de barras. También faltaba tratar lectores físicos HID, validación de formato y generación de SKU.

