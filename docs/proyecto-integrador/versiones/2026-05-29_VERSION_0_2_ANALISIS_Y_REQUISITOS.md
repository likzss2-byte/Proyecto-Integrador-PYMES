# Versión 0.2 — Análisis y requisitos

Periodo de trabajo: 29 de mayo al 4 de junio de 2026  
Estado de la etapa: Finalizada como definición  
Versión anterior: 0.1  
Siguiente versión: 0.3


## Situación al iniciar

Recibimos de la versión 0.1 un problema claro, pero todavía sin requisitos ordenados. En esta etapa organizamos lo que esperábamos que hiciera el sistema, sin afirmar que ya estuviera implementado.

## Actores

- Administrador del negocio.
- Encargado de inventario.
- Encargado de compras.
- Usuario operativo que registra entradas o salidas.

## Historias de usuario

- Como encargado de inventario, queremos registrar productos para consultar existencias sin depender de hojas sueltas.
- Como usuario operativo, queremos escribir o escanear un código para encontrar un producto rápido.
- Como administrador, queremos evitar productos duplicados.
- Como encargado de almacén, queremos comparar inventario físico contra inventario teórico.
- Como responsable de compras, queremos registrar proveedores, pedidos y recepciones.

## Casos de uso definidos

1. Registrar producto manual.
2. Buscar producto por SKU o código.
3. Registrar proveedor.
4. Registrar entrada.
5. Registrar salida.
6. Registrar ajuste.
7. Realizar conteo físico.
8. Consultar faltantes y sobrantes.
9. Registrar pedido.
10. Registrar recepción.

## Requisitos funcionales definidos

Definimos como requisitos:

- Registrar productos manualmente.
- Registrar productos mediante código de barras.
- Generar automáticamente un SKU.
- Consultar información de productos mediante servicios externos.
- Evitar códigos de barras duplicados.
- Evitar SKU duplicados.
- Registrar proveedores.
- Controlar entradas.
- Controlar salidas.
- Registrar ajustes.
- Consultar existencias.
- Comparar inventario físico y teórico.
- Mostrar faltantes y sobrantes.
- Manejar lotes.
- Registrar caducidades.
- Registrar pedidos y recepciones.
- Permitir diferentes negocios.

En esta versión los requisitos quedaron definidos, no implementados.

## Requisitos no funcionales

Anotamos requisitos de usabilidad, integridad de datos, funcionamiento local, compatibilidad con Windows y posibilidad de crecer hacia Android. También dejamos planteada la mantenibilidad: separar interfaz, reglas y almacenamiento para evitar que toda la lógica quedara en una sola pantalla.

## Reglas de negocio iniciales

- Cada producto debía tener un identificador interno.
- No debían repetirse SKU ni códigos de barras dentro de un mismo negocio.
- Las entradas aumentan existencias.
- Las salidas disminuyen existencias.
- Los ajustes deben dejar evidencia.
- Un pedido no debe aumentar inventario hasta registrarse la recepción.
- El inventario físico es la cantidad contada; el inventario teórico es la cantidad registrada.

## Prioridades

| Prioridad | Requisitos |
|---|---|
| Alta | Productos, duplicados, entradas, salidas, existencias y ajustes. |
| Media | Proveedores, códigos de barras, API, conteos físicos, lotes y caducidades. |
| Baja inicial | Multiempresa completa, cámara en vivo, roles y reportes avanzados. |

## Criterios de aceptación iniciales

Consideramos aceptable que un primer prototipo permitiera registrar productos y consultarlos. Para una versión más avanzada, el sistema debía impedir duplicados, conservar movimientos y aprobar pruebas automatizadas.

## Fuera de alcance inicial

No incluimos servidor remoto, sincronización entre sucursales, facturación, punto de venta completo, roles avanzados ni autenticación formal.

## Primera matriz breve

| ID | Requisito | Estado en esta etapa |
|---|---|---|
| RF-001 | Registrar productos | Definido |
| RF-002 | Registrar por código | Definido |
| RF-003 | Evitar duplicados | Definido |
| RF-004 | Controlar movimientos | Definido |
| RF-005 | Comparar físico y teórico | Definido |
| RF-006 | Pedidos y recepciones | Definido |
| RF-007 | Varios negocios | Definido |

## Pendiente para la versión 0.3

Nos faltaba decidir con qué tecnología construiríamos el sistema y cómo guardaríamos datos sin depender completamente de internet.

