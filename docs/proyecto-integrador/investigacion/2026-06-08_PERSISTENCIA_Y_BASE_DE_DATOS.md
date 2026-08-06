# Investigación: persistencia y base de datos

Fecha documentada: 8 de junio de 2026

Documento reconstruido a partir de las evidencias actuales del proyecto.

## Problema que teníamos

El sistema necesitaba guardar inventario sin depender de un servidor desde el inicio. También necesitábamos transacciones para evitar que una operación incompleta dañara el stock.

## Opciones revisadas

- Archivos simples.
- SQLite local.
- Base remota con servidor.
- Entity Framework Core.

## Comparación

| Opción | Ventajas | Desventajas |
|---|---|---|
| Archivos JSON/CSV | Simples para prototipo. | Integridad limitada y riesgo de duplicados. |
| SQLite | Local, transaccional y embebida. | No sincroniza varios dispositivos por sí sola. |
| Servidor remoto | Centralización. | Requiere backend, hosting y conexión. |
| EF Core | Abstracción de datos. | No se encontró implementado en el repositorio. |

## Qué elegimos

Elegimos SQLite local. En el estado final aparece `sqlite-net-pcl`, `InventoryDatabase.cs`, `DatabaseMigrator.cs` y `DataBaseInitialize.cs`.

## Por qué

SQLite resolvía la necesidad de trabajar localmente y con transacciones. Además, facilitó pruebas automatizadas sin depender de un servidor.

## Evidencia actual

- Base `InventorySystem.db`.
- Migraciones por `PRAGMA user_version`.
- `PRAGMA foreign_keys=ON`.
- `PRAGMA journal_mode=WAL`.
- Respaldo previo en migraciones.
- Transacciones con commit y rollback.

## Consecuencias

La persistencia local redujo dependencia de internet. La consecuencia negativa es que una versión futura necesitaría estrategia de respaldo o sincronización si se usa en varios equipos.

## Relación con la siguiente versión

El diseño de datos de la versión 0.4 tomó esta decisión como base.

## Referencias consultadas

| Título | Organización | URL | Fecha de consulta |
|---|---|---|---|
| SQLite Documentation | SQLite | https://www.sqlite.org/docs.html | 2026-08-05 |
| Write-Ahead Logging | SQLite | https://www.sqlite.org/wal.html | 2026-08-05 |

