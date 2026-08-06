# Manual de usuario

Fecha: 5 de agosto de 2026

Documento reconstruido a partir de las evidencias actuales del proyecto.

## Abrir la aplicación

Abrimos la aplicación en Windows desde el entorno de desarrollo o desde el ejecutable generado. Al iniciar, la pantalla de carga prepara la base local y después muestra Inicio.

## Usar el menú

El menú lateral permite entrar a Inicio, Inventario, Nueva entrada, Pedidos y recepciones, Nuevo proveedor, Nueva venta, Nuevo producto y Proveedores.

## Registrar productos

1. Entramos a Nuevo producto.
2. Capturamos SKU interno.
3. Capturamos código de barras si existe.
4. Escribimos nombre, descripción y marca.
5. Seleccionamos unidad.
6. Indicamos stock mínimo, precio y modo de caducidad.
7. Guardamos.

Si el SKU o código ya existen, el sistema muestra error.

## Escribir o escanear códigos

Podemos escribir el código manualmente, usar un lector físico que funcione como teclado HID o presionar el botón **Escanear con cámara** en las pantallas donde aparece. En Windows no se abre la aplicación Cámara por separado; la vista previa se muestra dentro de Inventario PYMES.

Si el equipo tiene más de una cámara, el sistema muestra un selector para cambiar entre la cámara integrada y una cámara externa o USB. Después de detectar el código, el sistema regresa al formulario y coloca el código en el campo correspondiente.

Advertencia: en Windows verificamos que el botón abriera el escáner interno y que una cámara iniciara la captura. No dejamos registrada la decodificación de un código físico, una prueba específica con webcam USB ni una ejecución en dispositivo Android.

## Confirmar información externa

Cuando el sistema encuentra datos externos, revisamos nombre, descripción y marca. Confirmamos solo si corresponden al producto real. Si faltan datos, los completamos antes de guardar.

## Registrar proveedores

Entramos a Nuevo proveedor, capturamos empresa y datos de contacto, y guardamos. Después podemos consultarlo desde Proveedores.

## Registrar entradas

En Nueva entrada escribimos código o SKU, usamos lector HID o escaneamos con cámara. Después capturamos cantidad, costo, proveedor, lote y caducidad cuando corresponda. Al confirmar, el stock aumenta y queda movimiento.

## Registrar salidas

En Nueva venta escribimos código o SKU, usamos lector HID o escaneamos con cámara. Después capturamos cantidad. Al confirmar, el stock disminuye. Si no hay existencia suficiente, el sistema rechaza la operación salvo configuración interna.

## Realizar ajustes

Desde el detalle del producto capturamos ajuste o conteo simple y motivo. El ajuste modifica stock y registra movimiento.

## Consultar inventario

En Inventario buscamos por nombre, SKU o código. También podemos usar el botón de cámara para capturar un código y filtrar el inventario. Desde ahí abrimos detalles de producto, lotes y movimientos.

## Realizar conteo

Podemos usar conteo por proveedor, por marca u operativo. Podemos capturar el producto por código escrito, lector HID o cámara. Después capturamos cantidades físicas, guardamos avance y confirmamos cuando esté completo. Guardar avance no cambia stock.

## Interpretar faltantes y sobrantes

- Faltante: físico menor que teórico.
- Sobrante: físico mayor que teórico.
- Sin diferencia: ambos coinciden.

## Lotes y caducidades

Cuando un producto maneja caducidad, capturamos lote y fecha. El sistema registra alertas y consume lotes con criterio FEFO.

## Pedidos y recepciones

En Pedidos y recepciones creamos pedidos por proveedor. Las líneas del pedido y las recepciones permiten capturar producto por código escrito, lector HID o cámara. El pedido no aumenta inventario. El stock aumenta hasta registrar la recepción.

## Errores comunes

| Mensaje o situación | Acción |
|---|---|
| SKU duplicado | Buscar producto existente o usar otro SKU. |
| Código duplicado | Revisar si el producto ya está registrado. |
| Producto no encontrado | Verificar captura o registrar manualmente. |
| API sin respuesta | Capturar datos manualmente. |
| Cantidad inválida | Usar cantidad positiva. |
| Caducidad requerida | Capturar fecha cuando el producto lo exija. |
