# Investigación: diseño responsivo en .NET MAUI

Periodo documentado: 28 al 31 de julio de 2026

Documento reconstruido a partir de las evidencias actuales del proyecto.

## Problema que teníamos

Las pantallas de inventario, pedidos y formularios podían verse bien en una ventana, pero no necesariamente en otra. Necesitábamos revisar cómo adaptar listas, encabezados, botones, scroll y espacios.

## Opciones revisadas

- Diseños fijos.
- Scroll en formularios largos.
- `VisualStateManager`.
- Estilos reutilizables.
- Separar plantillas visuales.

## Qué elegimos

La evidencia final muestra uso de estilos, recursos y estados visuales (`Resources/Styles/Styles.xaml`, `InventoryPage.xaml`, `PurchaseOrdersPage.xaml`). También encontramos componentes visuales reutilizables en `VisualElementsTemplates/`.

## Ventajas

- Menos repetición visual.
- Mejor adaptación a ventanas distintas.
- Menú lateral más claro.

## Desventajas

- No encontramos pruebas automatizadas de responsividad.
- Algunas decisiones visuales siguen en XAML específico de pantalla.

## Evidencia actual

Los archivos modificados previamente en el árbol de trabajo incluyen `InventoryPage.xaml`, `PurchaseOrdersPage.xaml`, `AppShell.xaml`, `Styles.xaml`, `SearchBar.xaml` y `SortButton.xaml`. No los modificamos durante esta documentación.

## Relación con la siguiente versión

Esta revisión alimentó la validación final y las pruebas de regresión documentadas.

