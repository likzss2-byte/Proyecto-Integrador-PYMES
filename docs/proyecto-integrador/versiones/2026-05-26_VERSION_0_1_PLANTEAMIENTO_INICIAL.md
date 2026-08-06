# Versión 0.1 — Planteamiento inicial

Periodo de trabajo: 26 al 28 de mayo de 2026  
Estado de la etapa: Cerrada como planteamiento  
Versión anterior: No aplica  
Siguiente versión: 0.2


## Punto de partida

En esta primera etapa todavía no contábamos con una solución terminada. Partimos del problema general de una PYME que registra productos y movimientos de manera manual, normalmente en libretas, hojas de cálculo o notas separadas. Ese tipo de control puede funcionar al inicio, pero se vuelve difícil cuando aumentan productos, proveedores y ventas.

Nos enfocamos en entender el problema antes de elegir tecnología.

## Problema identificado

Detectamos estas necesidades iniciales:

- Registros manuales con errores de captura.
- Productos duplicados por nombres, SKU o códigos mal escritos.
- Falta de control claro de existencias.
- Diferencias entre inventario físico e inventario teórico.
- Dificultad para saber qué falta y qué sobra.
- Falta de trazabilidad en entradas, salidas y ajustes.
- Información dispersa de productos y proveedores.

## Justificación

Decidimos plantear un sistema de inventario porque el problema no era solo guardar productos. También necesitábamos registrar movimientos y conservar evidencia de por qué cambia el stock. Desde esta etapa entendimos que el sistema debía ayudar a reducir errores, pero sin depender de procesos complicados para el usuario.

## Objetivo general

Diseñar y construir una aplicación de inventario para una PYME que permita registrar productos, consultar existencias y preparar el control de entradas, salidas y ajustes.

## Objetivos específicos

- Definir los datos mínimos de un producto.
- Plantear una forma de evitar duplicados.
- Separar inventario físico e inventario teórico.
- Considerar el uso de códigos de barras.
- Identificar usuarios esperados.
- Preparar una base para integrar proveedores y movimientos en etapas posteriores.

## Usuarios esperados

Pensamos principalmente en personal administrativo, encargado de almacén, responsable de compras y dueño o administrador del negocio. No definimos todavía perfiles técnicos ni permisos detallados.

## Alcance inicial

El alcance inicial fue deliberadamente limitado:

- Registro básico de productos.
- Consulta de productos.
- Primer planteamiento de existencias.
- Posible captura por código de barras.
- Diseño inicial de navegación.

No presentamos como terminados módulos de API, cámara, pedidos, multiempresa, conteos físicos completos ni base de datos final.

## Restricciones iniciales

- El sistema debía ser entendible para usuarios no técnicos.
- Debíamos evitar depender desde el inicio de un servidor remoto.
- El registro manual debía seguir disponible.
- La solución debía poder crecer sin rehacer todo.

## Riesgos

El riesgo principal era construir una pantalla de productos sin resolver la trazabilidad del inventario. También vimos el riesgo de confundir pedido con entrada real, o inventario físico con existencias registradas.

## Primera propuesta

Planteamos una aplicación con pantallas de productos e inventario. En ese momento todavía no decidimos la arquitectura definitiva. Dejamos abierta la investigación sobre aplicación web, escritorio o multiplataforma.

## Pendiente para la versión 0.2

Necesitábamos convertir el planteamiento en requisitos funcionales, reglas de negocio y criterios de aceptación. También debíamos separar qué funciones serían obligatorias y cuáles quedarían como evolución.

