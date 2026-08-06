# Versión 0.8 — Proveedores, pedidos y negocios

Periodo de trabajo: 19 al 27 de julio de 2026  
Estado de la etapa: Incorporada con pendientes  
Versión anterior: 0.7  
Siguiente versión: 0.9

## Punto de partida

Con movimientos y stock ya definidos, necesitábamos resolver la parte de compras: proveedores, pedidos y recepciones. También retomamos el requisito de varios negocios.

## Proveedores

Encontramos registro y consulta de proveedores (`NewPurveyorPage.xaml.cs`, `PurveyorFullPage.xaml.cs`, `SupplierRepository.cs`). También existe relación producto-proveedor mediante `product_suppliers`.

## Filtros por proveedor y marca

El inventario por modalidad aparece en rutas y pantallas de conteo (`SupplierInventoryPage`, `BrandInventoryPage`, `OperationalInventoryPage`, `InventoryCountPage.xaml.cs`). Esto permite trabajar conteos por proveedor, por marca o libres.

## Pedidos y recepciones

Los pedidos se manejan con `PurchaseOrderService.cs` y pantalla `PurchaseOrdersPage.xaml.cs`. El diseño evita confundir pedido con entrada: el pedido registra intención de compra; la recepción confirma entrada real y actualiza inventario.

## Costos, lotes y caducidades

Las líneas de pedido y recepción manejan cantidades, costos, lote y fecha de caducidad cuando aplica. La recepción puede crear lotes y movimientos.

## Diferentes negocios

El modelo final usa `business_id` en tablas principales y `BusinessService.cs` trabaja con un negocio predeterminado. No encontramos una pantalla completa para crear, cambiar o administrar negocios. Por eso marcamos multiempresa como parcial.

## Pruebas

Encontramos pruebas de pedidos, recepción parcial/completa, recepción idempotente, lotes por recepción, filtros por proveedor y marca. No encontramos pruebas de interfaz para estos formularios.

## Pendientes

Quedó pendiente una administración completa de negocios, pruebas de interfaz para pedidos y reportes de compras.

