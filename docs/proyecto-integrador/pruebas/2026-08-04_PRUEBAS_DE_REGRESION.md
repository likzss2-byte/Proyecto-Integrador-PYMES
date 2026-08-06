# Pruebas de regresión

Fecha documentada: 4 de agosto de 2026

Documento reconstruido a partir de las evidencias actuales del proyecto.

## Propósito

Antes del cierre necesitábamos comprobar que los cambios en interfaz, navegación y servicios no rompieran reglas ya cubiertas.

## Regresión automatizada disponible

La regresión automatizada real corresponde a `dotnet test`. Cubre reglas de negocio e infraestructura.

## Regresión manual propuesta

Dejamos como lista manual:

- Abrir aplicación.
- Navegar por menú lateral.
- Registrar producto.
- Consultar producto por SKU.
- Registrar entrada.
- Registrar salida.
- Crear conteo físico.
- Crear pedido.
- Recibir pedido.
- Revisar lotes y caducidades.

## Riesgo

Sin pruebas automatizadas UI, un cambio visual puede romper un flujo sin ser detectado por la regresión automatizada.



