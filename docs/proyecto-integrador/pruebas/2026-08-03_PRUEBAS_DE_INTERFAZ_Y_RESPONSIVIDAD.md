# Pruebas de interfaz y responsividad

Fecha documentada: 3 de agosto de 2026

Documento reconstruido a partir de las evidencias actuales del proyecto.

## Qué buscábamos validar

Queríamos revisar navegación, menú lateral, formularios, scroll, estados vacíos, botones y adaptación a ventanas pequeñas.

## Evidencia encontrada

Encontramos diseño responsivo y estilos en:

- `Resources/Styles/Styles.xaml`.
- `AppShell.xaml`.
- `InventoryPage.xaml`.
- `PurchaseOrdersPage.xaml`.
- `VisualElementsTemplates/SearchBar.xaml`.
- `VisualElementsTemplates/SortButton.xaml`.

## Pruebas no encontradas como automatizadas

No encontramos pruebas automatizadas de interfaz. Por eso estos casos quedan como pruebas manuales propuestas:

- Abrir y cerrar menú lateral.
- Revisar botón de tres barras.
- Navegar a Inventario, Productos, Proveedores, Entradas, Salidas y Pedidos.
- Probar formularios con campos vacíos.
- Revisar scroll en formularios largos.
- Cambiar tamaño de ventana.
- Revisar tablas/listas sin datos.
- Verificar textos de botones recortados.

## Resultado

No marcamos estas pruebas como aprobadas porque no encontramos evidencia de una ejecución formal ni una suite UI.

## Pendiente

Agregar pruebas de interfaz o, al menos, una lista de verificación manual con capturas y resultados por pantalla.

