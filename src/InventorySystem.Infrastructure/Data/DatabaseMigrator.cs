using System.Globalization;
using SQLite;

namespace InventorySystem.Infrastructure.Data;

internal static class DatabaseMigrator
{
    public const int LatestSchemaVersion = 3;

    public static string? Migrate(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = new SQLiteConnection(
            databasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex,
            storeDateTimeAsTicks: false);
        connection.Execute("PRAGMA foreign_keys = ON;");
        _ = connection.ExecuteScalar<int>("PRAGMA busy_timeout = 5000;");
        _ = connection.ExecuteScalar<string>("PRAGMA journal_mode = WAL;");

        var version = connection.ExecuteScalar<int>("PRAGMA user_version;");
        if (version > LatestSchemaVersion)
        {
            throw new InvalidOperationException(
                $"La base usa el esquema {version}, pero esta aplicación solo reconoce hasta {LatestSchemaVersion}.");
        }

        string? backupPath = null;
        if (version < LatestSchemaVersion && HasUserTables(connection))
        {
            backupPath = CreateBackup(connection, databasePath);
        }

        while (version < LatestSchemaVersion)
        {
            var nextVersion = version + 1;
            connection.BeginTransaction();
            try
            {
                switch (nextVersion)
                {
                    case 1:
                        CreateCoreSchema(connection);
                        break;
                    case 2:
                        ImportLegacyDestinationData(connection);
                        break;
                    case 3:
                        CreateAuditGuards(connection);
                        break;
                    default:
                        throw new InvalidOperationException($"No existe la migración {nextVersion}.");
                }

                connection.Execute($"PRAGMA user_version = {nextVersion.ToString(CultureInfo.InvariantCulture)};");
                connection.Commit();
                version = nextVersion;
            }
            catch
            {
                connection.Rollback();
                throw;
            }
        }

        return backupPath;
    }

    private static bool HasUserTables(SQLiteConnection connection) =>
        connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';") > 0;

    private static string CreateBackup(SQLiteConnection connection, string databasePath)
    {
        _ = connection.Query<WalCheckpointRow>("PRAGMA wal_checkpoint(FULL);");
        var backupPath = databasePath + $".pre-migration-{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
        var escaped = backupPath.Replace("'", "''", StringComparison.Ordinal);
        connection.Execute($"VACUUM INTO '{escaped}';");
        return backupPath;
    }

    private static void CreateCoreSchema(SQLiteConnection connection)
    {
        ExecuteEach(connection,
            """
            CREATE TABLE IF NOT EXISTS businesses (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL COLLATE NOCASE,
                allow_negative_stock INTEGER NOT NULL DEFAULT 0 CHECK(allow_negative_stock IN (0,1)),
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS products (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                business_id INTEGER NOT NULL REFERENCES businesses(id),
                sku TEXT NOT NULL COLLATE NOCASE,
                barcode TEXT NULL COLLATE NOCASE,
                name TEXT NOT NULL COLLATE NOCASE,
                description TEXT NULL,
                brand TEXT NULL COLLATE NOCASE,
                unit_of_measure INTEGER NOT NULL DEFAULT 0 CHECK(unit_of_measure BETWEEN 0 AND 2),
                stock_milli INTEGER NOT NULL DEFAULT 0,
                minimum_stock_milli INTEGER NOT NULL DEFAULT 0 CHECK(minimum_stock_milli >= 0),
                sale_price_basis INTEGER NOT NULL DEFAULT 0 CHECK(sale_price_basis >= 0),
                active INTEGER NOT NULL DEFAULT 1 CHECK(active IN (0,1)),
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_products_business_sku ON products(business_id, sku);",
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_products_business_barcode ON products(business_id, barcode) WHERE barcode IS NOT NULL AND trim(barcode) <> '';",
            "CREATE INDEX IF NOT EXISTS ix_products_name ON products(business_id, name);",
            "CREATE INDEX IF NOT EXISTS ix_products_brand ON products(business_id, brand);",
            """
            CREATE TABLE IF NOT EXISTS suppliers (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                business_id INTEGER NOT NULL REFERENCES businesses(id),
                company_name TEXT NOT NULL COLLATE NOCASE,
                contact_name TEXT NULL,
                phone TEXT NULL,
                email TEXT NULL COLLATE NOCASE,
                country TEXT NULL,
                state TEXT NULL,
                address TEXT NULL,
                notes TEXT NULL,
                active INTEGER NOT NULL DEFAULT 1 CHECK(active IN (0,1)),
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_suppliers_business_company ON suppliers(business_id, company_name);",
            """
            CREATE TABLE IF NOT EXISTS product_suppliers (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                product_id INTEGER NOT NULL REFERENCES products(id),
                supplier_id INTEGER NOT NULL REFERENCES suppliers(id),
                supplier_sku TEXT NULL COLLATE NOCASE,
                reference_cost_basis INTEGER NULL CHECK(reference_cost_basis IS NULL OR reference_cost_basis >= 0),
                active INTEGER NOT NULL DEFAULT 1 CHECK(active IN (0,1)),
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                UNIQUE(product_id, supplier_id)
            );
            """,
            "CREATE INDEX IF NOT EXISTS ix_product_suppliers_supplier ON product_suppliers(supplier_id, active);",
            """
            CREATE TABLE IF NOT EXISTS inventory_documents (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                business_id INTEGER NOT NULL REFERENCES businesses(id),
                document_type INTEGER NOT NULL CHECK(document_type IN (0,1)),
                status INTEGER NOT NULL DEFAULT 0 CHECK(status BETWEEN 0 AND 2),
                reference TEXT NOT NULL COLLATE NOCASE,
                supplier_id INTEGER NULL REFERENCES suppliers(id),
                notes TEXT NULL,
                total_basis INTEGER NOT NULL DEFAULT 0 CHECK(total_basis >= 0),
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                confirmed_at TEXT NULL,
                cancelled_at TEXT NULL,
                UNIQUE(business_id, reference)
            );
            """,
            "CREATE INDEX IF NOT EXISTS ix_documents_status ON inventory_documents(business_id, document_type, status, created_at DESC);",
            """
            CREATE TABLE IF NOT EXISTS inventory_document_lines (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                document_id INTEGER NOT NULL REFERENCES inventory_documents(id),
                product_id INTEGER NOT NULL REFERENCES products(id),
                quantity_milli INTEGER NOT NULL CHECK(quantity_milli > 0),
                unit_price_basis INTEGER NOT NULL DEFAULT 0 CHECK(unit_price_basis >= 0),
                UNIQUE(document_id, product_id)
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS inventory_movements (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                business_id INTEGER NOT NULL REFERENCES businesses(id),
                product_id INTEGER NOT NULL REFERENCES products(id),
                movement_type INTEGER NOT NULL CHECK(movement_type BETWEEN 0 AND 7),
                quantity_milli INTEGER NOT NULL,
                previous_stock_milli INTEGER NOT NULL,
                resulting_stock_milli INTEGER NOT NULL,
                reference TEXT NOT NULL,
                reason TEXT NULL,
                occurred_at TEXT NOT NULL
            );
            """,
            "CREATE INDEX IF NOT EXISTS ix_movements_product ON inventory_movements(product_id, occurred_at DESC);",
            "CREATE INDEX IF NOT EXISTS ix_movements_date ON inventory_movements(business_id, occurred_at DESC);",
            """
            CREATE TABLE IF NOT EXISTS inventory_counts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                business_id INTEGER NOT NULL REFERENCES businesses(id),
                reference TEXT NOT NULL COLLATE NOCASE,
                status INTEGER NOT NULL DEFAULT 0 CHECK(status IN (0,1)),
                notes TEXT NULL,
                counted_at TEXT NOT NULL,
                created_at TEXT NOT NULL,
                confirmed_at TEXT NULL,
                UNIQUE(business_id, reference)
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS inventory_count_lines (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                count_id INTEGER NOT NULL REFERENCES inventory_counts(id),
                product_id INTEGER NOT NULL REFERENCES products(id),
                theoretical_milli INTEGER NOT NULL,
                physical_milli INTEGER NOT NULL CHECK(physical_milli >= 0),
                difference_milli INTEGER NOT NULL,
                UNIQUE(count_id, product_id)
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS recent_product_queries (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                business_id INTEGER NOT NULL REFERENCES businesses(id),
                code TEXT NOT NULL,
                product_id INTEGER NULL REFERENCES products(id),
                source TEXT NOT NULL,
                queried_at TEXT NOT NULL
            );
            """,
            "CREATE INDEX IF NOT EXISTS ix_recent_queries ON recent_product_queries(business_id, queried_at DESC);",
            """
            CREATE TABLE IF NOT EXISTS legacy_imports (
                source_table TEXT NOT NULL,
                source_id TEXT NOT NULL,
                target_table TEXT NOT NULL,
                target_id INTEGER NOT NULL,
                imported_at TEXT NOT NULL,
                PRIMARY KEY(source_table, source_id, target_table)
            );
            """);

        var now = SqliteValues.Date(DateTime.UtcNow);
        connection.Execute(
            "INSERT INTO businesses(name,allow_negative_stock,created_at,updated_at) SELECT ?,0,?,? WHERE NOT EXISTS(SELECT 1 FROM businesses);",
            "Mi negocio",
            now,
            now);
    }

    private static void ImportLegacyDestinationData(SQLiteConnection connection)
    {
        var businessId = connection.ExecuteScalar<long>("SELECT id FROM businesses ORDER BY id LIMIT 1;");
        var now = SqliteValues.Date(DateTime.UtcNow);

        if (TableExists(connection, "Item") && ColumnExists(connection, "Item", "ItemID"))
        {
            connection.Execute(
                """
                INSERT OR IGNORE INTO products(
                    business_id,sku,barcode,name,description,brand,unit_of_measure,stock_milli,
                    minimum_stock_milli,sale_price_basis,active,created_at,updated_at)
                SELECT ?, 'LEGACY-' || printf('%06d',ItemID), NULL, trim(ItemName), ItemDescription, NULL, 0,
                       CAST(COALESCE(Stock,0) * 1000 AS INTEGER), 0,
                       CAST(round(COALESCE(SalePrice,0) * 10000) AS INTEGER), 1,
                       COALESCE(ModifiedAt,?), COALESCE(ModifiedAt,?)
                FROM Item;
                """,
                businessId,
                now,
                now);
            connection.Execute(
                """
                INSERT OR IGNORE INTO legacy_imports(source_table,source_id,target_table,target_id,imported_at)
                SELECT 'Item',CAST(i.ItemID AS TEXT),'products',p.id,?
                FROM Item i JOIN products p ON p.business_id=? AND p.sku='LEGACY-' || printf('%06d',i.ItemID);
                """,
                now,
                businessId);
        }

        if (TableExists(connection, "Purveyor") && ColumnExists(connection, "Purveyor", "PurveyorID"))
        {
            connection.Execute(
                """
                INSERT OR IGNORE INTO suppliers(
                    business_id,company_name,contact_name,phone,email,country,state,address,notes,active,created_at,updated_at)
                SELECT ?,trim(p.CompanyRegisteredName),NULL,
                       (SELECT CAST(n.PhoneNumber AS TEXT) FROM PurveyorPhoneNumber n WHERE n.PurveyorID=p.PurveyorID ORDER BY n.PurveyorPhoneNumberID LIMIT 1),
                       (SELECT e.Email FROM PurveyorEmail e WHERE e.PurveyorID=p.PurveyorID ORDER BY e.PurveyorPhoneNumberID LIMIT 1),
                       (SELECT a.Country FROM PurveyorAddress a WHERE a.PurveyorID=p.PurveyorID ORDER BY a.PurveyorAddressID LIMIT 1),
                       (SELECT a.State FROM PurveyorAddress a WHERE a.PurveyorID=p.PurveyorID ORDER BY a.PurveyorAddressID LIMIT 1),
                       (SELECT trim(COALESCE(a.Street,'') || ' ' || COALESCE(a.Neighborhood,'') || ' ' || COALESCE(a.PostalCode,'') || ' ' || COALESCE(a.AditionalReferences,'')) FROM PurveyorAddress a WHERE a.PurveyorID=p.PurveyorID ORDER BY a.PurveyorAddressID LIMIT 1),
                       'Importado del esquema original',1,?,?
                FROM Purveyor p;
                """,
                businessId,
                now,
                now);
            connection.Execute(
                """
                INSERT OR IGNORE INTO legacy_imports(source_table,source_id,target_table,target_id,imported_at)
                SELECT 'Purveyor',CAST(p.PurveyorID AS TEXT),'suppliers',s.id,?
                FROM Purveyor p JOIN suppliers s ON s.business_id=? AND s.company_name=p.CompanyRegisteredName COLLATE NOCASE;
                """,
                now,
                businessId);
        }

        if (TableExists(connection, "ItemPurveyor"))
        {
            connection.Execute(
                """
                INSERT OR IGNORE INTO product_suppliers(product_id,supplier_id,supplier_sku,reference_cost_basis,active,created_at,updated_at)
                SELECT pi.target_id,si.target_id,NULL,NULL,1,?,?
                FROM ItemPurveyor r
                JOIN legacy_imports pi ON pi.source_table='Item' AND pi.source_id=CAST(r.ItemID AS TEXT) AND pi.target_table='products'
                JOIN legacy_imports si ON si.source_table='Purveyor' AND si.source_id=CAST(r.PurveyorID AS TEXT) AND si.target_table='suppliers';
                """,
                now,
                now);
        }

        if (TableExists(connection, "Sale") && TableExists(connection, "SaleIncludes"))
        {
            connection.Execute(
                """
                INSERT OR IGNORE INTO inventory_documents(
                    business_id,document_type,status,reference,supplier_id,notes,total_basis,created_at,updated_at,confirmed_at,cancelled_at)
                SELECT ?,1,1,'LEGACY-SALE-' || SaleID,NULL,'Importada del esquema original',
                       CAST(round(COALESCE(SaleTotal,0)*10000) AS INTEGER),TransactionDate,TransactionDate,TransactionDate,NULL
                FROM Sale;
                """,
                businessId);
            connection.Execute(
                """
                INSERT OR IGNORE INTO inventory_document_lines(document_id,product_id,quantity_milli,unit_price_basis)
                SELECT d.id,pi.target_id,CAST(si.ItemSaleQuantity*1000 AS INTEGER),
                       CASE WHEN si.ItemSaleQuantity=0 THEN 0 ELSE CAST(d.total_basis/si.ItemSaleQuantity AS INTEGER) END
                FROM SaleIncludes si
                JOIN inventory_documents d ON d.business_id=? AND d.reference='LEGACY-SALE-' || si.SaleID
                JOIN legacy_imports pi ON pi.source_table='Item' AND pi.source_id=CAST(si.ItemID AS TEXT) AND pi.target_table='products'
                WHERE si.ItemSaleQuantity>0;
                """,
                businessId);
        }
    }

    private static void CreateAuditGuards(SQLiteConnection connection)
    {
        connection.Execute(
            """
            CREATE TRIGGER IF NOT EXISTS trg_inventory_movements_no_update
            BEFORE UPDATE ON inventory_movements
            BEGIN
                SELECT RAISE(ABORT, 'Los movimientos de inventario son inmutables');
            END;
            """);
        connection.Execute(
            """
            CREATE TRIGGER IF NOT EXISTS trg_inventory_movements_no_delete
            BEFORE DELETE ON inventory_movements
            BEGIN
                SELECT RAISE(ABORT, 'Los movimientos de inventario son inmutables');
            END;
            """);
    }

    private static bool TableExists(SQLiteConnection connection, string tableName) =>
        connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=? COLLATE NOCASE;",
            tableName) > 0;

    private static bool ColumnExists(SQLiteConnection connection, string tableName, string columnName)
    {
        var safeTable = tableName.Replace("\"", "\"\"", StringComparison.Ordinal);
        return connection.Query<ColumnInfo>($"PRAGMA table_info(\"{safeTable}\");")
            .Any(column => string.Equals(column.Name, columnName, StringComparison.OrdinalIgnoreCase));
    }

    private static void ExecuteEach(SQLiteConnection connection, params string[] statements)
    {
        foreach (var statement in statements)
        {
            connection.Execute(statement);
        }
    }

    private sealed class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class WalCheckpointRow
    {
        public int Busy { get; set; }
        public int Log { get; set; }
        public int Checkpointed { get; set; }
    }
}
