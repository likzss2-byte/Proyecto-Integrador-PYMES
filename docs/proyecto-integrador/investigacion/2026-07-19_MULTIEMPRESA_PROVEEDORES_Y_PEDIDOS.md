# Investigación: multiempresa, proveedores y pedidos

Periodo documentado: 19 al 23 de julio de 2026

Documento reconstruido a partir de las evidencias actuales del proyecto.

## Problema que teníamos

Después de controlar inventario necesitábamos atender compras y separar información por negocio. También teníamos que decidir cómo relacionar proveedores con productos.

## Opciones revisadas

- Proveedores como texto libre.
- Proveedores como entidad propia.
- Una base por negocio.
- Una base con `business_id`.
- Pedidos que afectan stock inmediatamente.
- Pedidos que afectan stock solo al recibir.

## Qué elegimos

La evidencia final muestra proveedores como entidad propia, relación producto-proveedor, pedidos separados de recepciones y datos asociados a `business_id`.

## Por qué

Separar pedido de recepción evita aumentar stock antes de tener mercancía. Usar `business_id` prepara el sistema para varios negocios, aunque la interfaz final no lo administra completamente.

## Evidencia actual

- `SupplierRepository.cs`.
- `PurchaseOrderService.cs`.
- `DatabaseMigrator.cs`.
- `BusinessService.cs`.
- Pantallas `PurchaseOrdersPage`, `NewPurveyorPage` y modalidades de conteo.

## Consecuencias

El modelo quedó preparado para compras reales. Lo parcial es la multiempresa desde UI: existe estructura, pero no encontramos alta y cambio completo de negocio.

## Relación con la siguiente versión

Al crecer los módulos, la siguiente etapa debía atender interfaz, navegación y responsividad.

