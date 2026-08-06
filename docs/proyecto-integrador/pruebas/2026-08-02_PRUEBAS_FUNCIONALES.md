# Pruebas funcionales

Fecha documentada: 2 de agosto de 2026

Documento reconstruido a partir de las evidencias actuales del proyecto.

## Qué revisamos

Relacionamos las pruebas funcionales con el proyecto de pruebas real (`tests/InventorySystem.Tests/InventoryLogicTests.cs`). No inventamos ejecuciones manuales históricas.

## Pruebas encontradas

| Función | Evidencia | Estado |
|---|---|---|
| Registro de producto | Pruebas de creación y consulta | Encontrada |
| SKU duplicado | `Sku_debe_ser_unico` | Encontrada |
| Código duplicado | Prueba de código único | Encontrada |
| Búsqueda por código/SKU | Pruebas de `FindByCodeAsync` | Encontrada |
| Entradas | Pruebas de entrada y cancelación | Encontrada |
| Salidas | Pruebas de venta y cancelación | Encontrada |
| Ajustes | Motivo obligatorio y ajuste | Encontrada |
| Existencias | Cambios de stock | Encontrada |
| Conteos | Faltante, sobrante, cero, decimales | Encontrada |
| Lotes y caducidades | FEFO, vencidos y alertas | Encontrada |
| Proveedores | Relación producto-proveedor | Encontrada |
| Pedidos | Crear, confirmar, cancelar | Encontrada |
| Recepciones | Parcial/completa e idempotente | Encontrada |

## Pruebas propuestas no ejecutadas como manuales

- Registrar producto completo desde la interfaz.
- Escanear con lector USB real.
- Simular error de red real contra API.
- Recibir pedido desde interfaz y revisar lista.
- Validar mensajes visuales de error.

## Observación

Las pruebas funcionales automatizadas cubren reglas e infraestructura, no la interacción visual de usuario.

