# Plan de pruebas

Fecha documentada: 30 de julio de 2026

## Objetivo

Definimos este plan para comprobar productos, códigos, duplicados, inventario, lotes, proveedores, pedidos, recepciones, navegación y compilación. No marcamos como aprobada ninguna prueba que no hayamos ejecutado en la revisión final.

## Alcance

Incluimos:

- Pruebas automatizadas encontradas.
- Pruebas ejecutadas ahora.
- Pruebas manuales documentadas como propuestas.
- Pruebas no ejecutadas.

## Casos principales

| Área | Prueba prevista | Tipo |
|---|---|---|
| Productos | Registro manual y validaciones | Automatizada y manual propuesta |
| Código de barras | Búsqueda local, duplicados, lectura HID, cámara Android y cámara Windows | Automatizada parcial/manual propuesta |
| API | Producto encontrado, no encontrado, error de red | Automatizada parcial/manual propuesta |
| Inventario | Entradas, salidas, ajustes y existencias | Automatizada |
| Conteos | Faltantes, sobrantes y confirmación | Automatizada |
| Lotes | Caducidad, vencidos y FEFO | Automatizada |
| Proveedores | Registro y relación producto-proveedor | Automatizada |
| Pedidos | Pedido, recepción e idempotencia | Automatizada |
| Negocios | Aislamiento por `business_id` | Parcial |
| UI | Navegación, formularios, responsividad | Manual propuesta |
| Cámara | Cámara integrada Windows, webcam USB, cámara Android, permisos, cambio de cámara y retorno del código al formulario | Manual propuesta |
| Compilación | `dotnet build` | Ejecutada |

## Criterios de aceptación

- Compilación sin errores.
- Pruebas automatizadas aprobadas.
- Compilación Android y Windows sin errores.
- Duplicados rechazados.
- Stock consistente después de entradas, salidas y ajustes.
- Pedidos no afectan stock hasta recepción.
- Conteos no cambian stock hasta confirmarse.

## Criterios de rechazo

- Error de compilación.
- Pruebas críticas fallidas.
- Duplicados permitidos.
- Stock negativo no permitido aceptado.
- Recepción duplicada que actualice stock dos veces.

## Pendientes del plan

Nos faltó automatizar interfaz, probar lector físico real, validar Android en dispositivo, probar cámara integrada de laptop, probar webcam USB o externa en Windows y probar red desconectada con una ejecución manual documentada.
