using System.Globalization;
using InventorySystem.Domain;
using InventorySystem.Infrastructure.Data;
using InventorySystem.Infrastructure.Repositories;
using SQLite;

namespace InventorySystem.Infrastructure.Services;

public sealed class InventoryCountSessionService
{
    private readonly InventoryDatabase _database;

    public InventoryCountSessionService(InventoryDatabase database)
    {
        _database = database;
    }

    public Task<InventoryCount> CreateAsync(
        long businessId,
        InventoryCountSessionInput input,
        CancellationToken cancellationToken = default) =>
        _database.WriteAsync(connection =>
        {
            var (supplierId, brand, products) = PrepareScope(connection, businessId, input);
            var now = SqliteValues.Date(DateTime.UtcNow);
            var reference = $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
            connection.Execute(
                """
                INSERT INTO inventory_counts(
                    business_id,reference,inventory_type,supplier_id,brand,status,notes,
                    started_at,counted_at,created_at,updated_at,confirmed_at,cancelled_at)
                VALUES(?,?,?,?,?,?,?,?,?,?,?,NULL,NULL);
                """,
                businessId,
                reference,
                (int)input.Type,
                supplierId,
                ProductRepository.DbText(brand),
                (int)InventoryCountStatus.InProgress,
                ProductRepository.DbText(input.Notes),
                now,
                now,
                now,
                now);
            var countId = connection.ExecuteScalar<long>("SELECT last_insert_rowid();");
            foreach (var product in products)
            {
                InsertProductLine(connection, countId, product);
            }

            return GetSession(connection, businessId, countId)!;
        }, cancellationToken);

    public Task<InventoryCount?> GetAsync(
        long businessId,
        long sessionId,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync(
            connection => GetSession(connection, businessId, sessionId),
            cancellationToken);

    public Task<IReadOnlyList<InventoryCount>> GetOpenAsync(
        long businessId,
        InventoryCountType? type = null,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync<IReadOnlyList<InventoryCount>>(connection =>
        {
            var ids = type.HasValue
                ? connection.Query<IdRow>(
                    """
                    SELECT id Id FROM inventory_counts
                    WHERE business_id=? AND inventory_type=? AND status IN (?,?)
                    ORDER BY updated_at DESC,id DESC;
                    """,
                    businessId,
                    (int)type.Value,
                    (int)InventoryCountStatus.Draft,
                    (int)InventoryCountStatus.InProgress)
                : connection.Query<IdRow>(
                    """
                    SELECT id Id FROM inventory_counts
                    WHERE business_id=? AND status IN (?,?)
                    ORDER BY updated_at DESC,id DESC;
                    """,
                    businessId,
                    (int)InventoryCountStatus.Draft,
                    (int)InventoryCountStatus.InProgress);
            return ids.Select(row => GetSession(connection, businessId, row.Id)!).ToArray();
        }, cancellationToken);

    public Task<IReadOnlyList<InventoryCount>> GetHistoryAsync(
        long businessId,
        int limit = 50,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync<IReadOnlyList<InventoryCount>>(connection =>
        {
            var ids = connection.Query<IdRow>(
                """
                SELECT id Id FROM inventory_counts
                WHERE business_id=?
                ORDER BY created_at DESC,id DESC LIMIT ?;
                """,
                businessId,
                Math.Clamp(limit, 1, 500));
            return ids.Select(row => GetSession(connection, businessId, row.Id)!).ToArray();
        }, cancellationToken);

    public Task<InventoryCount> AddProductAsync(
        long businessId,
        long sessionId,
        long productId,
        CancellationToken cancellationToken = default) =>
        _database.WriteAsync(connection =>
        {
            var session = RequireEditable(connection, businessId, sessionId);
            if (session.Type != InventoryCountType.FreeOperational)
            {
                throw new InventoryRuleException("Solo el inventario operativo permite agregar productos manualmente.");
            }

            if (session.Lines.Any(line => line.ProductId == productId))
            {
                throw new InventoryRuleException("El producto ya está incluido en esta sesión.");
            }

            var product = ProductRepository.GetRow(connection, businessId, productId)
                ?? throw new InventoryRuleException("El producto no existe.");
            if (product.Active != 1)
            {
                throw new InventoryRuleException("No se puede agregar un producto inactivo.");
            }

            InsertProductLine(connection, sessionId, product);
            Touch(connection, sessionId);
            return GetSession(connection, businessId, sessionId)!;
        }, cancellationToken);

    public Task<InventoryCount> RemoveProductAsync(
        long businessId,
        long sessionId,
        long productId,
        CancellationToken cancellationToken = default) =>
        _database.WriteAsync(connection =>
        {
            var session = RequireEditable(connection, businessId, sessionId);
            if (session.Type != InventoryCountType.FreeOperational)
            {
                throw new InventoryRuleException("Los productos del filtro no pueden quitarse de esta modalidad.");
            }

            var lineId = connection.ExecuteScalar<long>(
                "SELECT COALESCE(MAX(id),0) FROM inventory_count_lines WHERE count_id=? AND product_id=?;",
                sessionId,
                productId);
            if (lineId == 0)
            {
                throw new InventoryRuleException("El producto no pertenece a la sesión.");
            }

            connection.Execute("DELETE FROM inventory_count_lot_lines WHERE count_line_id=?;", lineId);
            connection.Execute("DELETE FROM inventory_count_lines WHERE id=?;", lineId);
            Touch(connection, sessionId);
            return GetSession(connection, businessId, sessionId)!;
        }, cancellationToken);

    public Task<InventoryCount> SetPhysicalQuantityAsync(
        long businessId,
        long sessionId,
        long productId,
        decimal physicalQuantity,
        string? observations = null,
        CancellationToken cancellationToken = default) =>
        _database.WriteAsync(connection =>
        {
            var session = RequireEditable(connection, businessId, sessionId);
            var line = session.Lines.SingleOrDefault(item => item.ProductId == productId)
                ?? throw new InventoryRuleException("El producto no pertenece a la sesión.");
            ValidatePhysicalQuantity(physicalQuantity, line.UnitOfMeasure);
            if (line.CountByLot)
            {
                throw new InventoryRuleException("Este producto se está contando por lote. Captura las cantidades en el detalle de lotes.");
            }

            var normalized = InventoryRules.NormalizeQuantity(physicalQuantity);
            var now = SqliteValues.Date(DateTime.UtcNow);
            connection.Execute(
                """
                UPDATE inventory_count_lines
                SET physical_milli=?,difference_milli=?,counted=1,counted_at=?,observations=?
                WHERE id=? AND count_id=?;
                """,
                SqliteValues.ToMilli(normalized),
                SqliteValues.ToMilli(normalized - line.TheoreticalStock),
                now,
                ProductRepository.DbText(observations),
                line.Id,
                sessionId);
            Touch(connection, sessionId, now);
            return GetSession(connection, businessId, sessionId)!;
        }, cancellationToken);

    public Task<InventoryCount> BeginLotCountAsync(
        long businessId,
        long sessionId,
        long productId,
        CancellationToken cancellationToken = default) =>
        _database.WriteAsync(connection =>
        {
            var session = RequireEditable(connection, businessId, sessionId);
            var line = session.Lines.SingleOrDefault(item => item.ProductId == productId)
                ?? throw new InventoryRuleException("El producto no pertenece a la sesión.");
            if (line.ExpirationMode != ExpirationMode.Tracked)
            {
                throw new InventoryRuleException("El producto no controla caducidad por lote.");
            }

            if (connection.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM inventory_count_lot_lines WHERE count_line_id=?;",
                    line.Id) == 0)
            {
                var lots = connection.Query<LotSnapshotRow>(
                    """
                    SELECT id Id,quantity_milli QuantityMilli FROM inventory_lots
                    WHERE product_id=? AND quantity_milli>0
                    ORDER BY CASE WHEN expiration_date IS NULL THEN 1 ELSE 0 END,expiration_date,received_at,id;
                    """,
                    productId);
                if (lots.Count == 0)
                {
                    throw new InventoryRuleException("El producto no tiene lotes con existencia para contar.");
                }

                foreach (var lot in lots)
                {
                    connection.Execute(
                        """
                        INSERT INTO inventory_count_lot_lines(
                            count_line_id,lot_id,theoretical_milli,physical_milli,counted,counted_at,observations)
                        VALUES(?,?,?,NULL,0,NULL,NULL);
                        """,
                        line.Id,
                        lot.Id,
                        lot.QuantityMilli);
                }
            }

            connection.Execute(
                """
                UPDATE inventory_count_lines
                SET count_by_lot=1,physical_milli=NULL,difference_milli=NULL,counted=0,counted_at=NULL
                WHERE id=?;
                """,
                line.Id);
            Touch(connection, sessionId);
            return GetSession(connection, businessId, sessionId)!;
        }, cancellationToken);

    public Task<InventoryCount> SetLotPhysicalQuantityAsync(
        long businessId,
        long sessionId,
        long countLotLineId,
        decimal physicalQuantity,
        string? observations = null,
        CancellationToken cancellationToken = default) =>
        _database.WriteAsync(connection =>
        {
            var session = RequireEditable(connection, businessId, sessionId);
            var lotLine = connection.Query<CountLotOwnerRow>(
                    """
                    SELECT ll.id Id,ll.count_line_id CountLineId,l.product_id ProductId,p.unit_of_measure UnitOfMeasure
                    FROM inventory_count_lot_lines ll
                    JOIN inventory_count_lines cl ON cl.id=ll.count_line_id
                    JOIN inventory_lots l ON l.id=ll.lot_id
                    JOIN products p ON p.id=l.product_id
                    WHERE ll.id=? AND cl.count_id=?;
                    """,
                    countLotLineId,
                    sessionId)
                .FirstOrDefault()
                ?? throw new InventoryRuleException("El lote no pertenece a la sesión.");
            ValidatePhysicalQuantity(physicalQuantity, (UnitOfMeasure)lotLine.UnitOfMeasure);
            var normalized = InventoryRules.NormalizeQuantity(physicalQuantity);
            var now = SqliteValues.Date(DateTime.UtcNow);
            connection.Execute(
                """
                UPDATE inventory_count_lot_lines
                SET physical_milli=?,counted=1,counted_at=?,observations=? WHERE id=?;
                """,
                SqliteValues.ToMilli(normalized),
                now,
                ProductRepository.DbText(observations),
                countLotLineId);

            var pending = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM inventory_count_lot_lines WHERE count_line_id=? AND counted=0;",
                lotLine.CountLineId);
            if (pending == 0)
            {
                var physicalMilli = connection.ExecuteScalar<long>(
                    "SELECT COALESCE(SUM(physical_milli),0) FROM inventory_count_lot_lines WHERE count_line_id=?;",
                    lotLine.CountLineId);
                var theoreticalMilli = connection.ExecuteScalar<long>(
                    "SELECT theoretical_milli FROM inventory_count_lines WHERE id=?;",
                    lotLine.CountLineId);
                connection.Execute(
                    """
                    UPDATE inventory_count_lines
                    SET physical_milli=?,difference_milli=?,counted=1,counted_at=? WHERE id=?;
                    """,
                    physicalMilli,
                    physicalMilli - theoreticalMilli,
                    now,
                    lotLine.CountLineId);
            }

            Touch(connection, sessionId, now);
            return GetSession(connection, businessId, sessionId)!;
        }, cancellationToken);

    public Task<InventoryCount> SaveProgressAsync(
        long businessId,
        long sessionId,
        string? notes,
        CancellationToken cancellationToken = default) =>
        _database.WriteAsync(connection =>
        {
            RequireEditable(connection, businessId, sessionId);
            connection.Execute(
                "UPDATE inventory_counts SET notes=?,updated_at=? WHERE id=? AND business_id=?;",
                ProductRepository.DbText(notes),
                SqliteValues.Date(DateTime.UtcNow),
                sessionId,
                businessId);
            return GetSession(connection, businessId, sessionId)!;
        }, cancellationToken);

    public Task<InventoryCountSummary> GetSummaryAsync(
        long businessId,
        long sessionId,
        CancellationToken cancellationToken = default) =>
        _database.ReadAsync(connection =>
        {
            var session = GetSession(connection, businessId, sessionId)
                ?? throw new InventoryRuleException("La sesión no existe.");
            return BuildSummary(session);
        }, cancellationToken);

    public Task<InventoryCount> ConfirmAsync(
        long businessId,
        long sessionId,
        bool allowIncomplete = false,
        CancellationToken cancellationToken = default) =>
        _database.WriteAsync(connection =>
        {
            var session = RequireEditable(connection, businessId, sessionId);
            if (session.Lines.Count == 0)
            {
                throw new InventoryRuleException("Agrega al menos un producto antes de confirmar.");
            }

            if (session.PendingProducts > 0 && !allowIncomplete)
            {
                throw new InventoryRuleException("La sesión tiene productos pendientes. Confirma explícitamente si deseas finalizarla incompleta.");
            }

            var countedLines = session.Lines.Where(line => line.Counted).ToArray();
            if (countedLines.Length == 0)
            {
                throw new InventoryRuleException("La sesión no contiene productos contados.");
            }

            ValidateBeforeConfirmation(connection, businessId, countedLines);
            var now = SqliteValues.Date(DateTime.UtcNow);
            foreach (var line in countedLines)
            {
                if (line.CountByLot)
                {
                    ApplyLotAdjustments(connection, businessId, session, line, now);
                }
                else
                {
                    ApplyProductAdjustment(connection, businessId, session, line, now);
                }
            }

            var changed = connection.Execute(
                """
                UPDATE inventory_counts
                SET status=?,confirmed_at=?,updated_at=?
                WHERE id=? AND business_id=? AND status IN (?,?);
                """,
                (int)InventoryCountStatus.Completed,
                now,
                now,
                sessionId,
                businessId,
                (int)InventoryCountStatus.Draft,
                (int)InventoryCountStatus.InProgress);
            if (changed != 1)
            {
                throw new InventoryRuleException("La sesión cambió de estado y no pudo confirmarse.");
            }

            return GetSession(connection, businessId, sessionId)!;
        }, cancellationToken);

    public Task<InventoryCount> CancelAsync(
        long businessId,
        long sessionId,
        string? notes = null,
        CancellationToken cancellationToken = default) =>
        _database.WriteAsync(connection =>
        {
            RequireEditable(connection, businessId, sessionId);
            var now = SqliteValues.Date(DateTime.UtcNow);
            var changed = connection.Execute(
                """
                UPDATE inventory_counts SET status=?,notes=COALESCE(?,notes),cancelled_at=?,updated_at=?
                WHERE id=? AND business_id=? AND status IN (?,?);
                """,
                (int)InventoryCountStatus.Cancelled,
                ProductRepository.DbText(notes),
                now,
                now,
                sessionId,
                businessId,
                (int)InventoryCountStatus.Draft,
                (int)InventoryCountStatus.InProgress);
            if (changed != 1)
            {
                throw new InventoryRuleException("La sesión no pudo cancelarse.");
            }

            return GetSession(connection, businessId, sessionId)!;
        }, cancellationToken);

    public static InventoryCountSummary BuildSummary(InventoryCount session) =>
        new(
            session.Lines.Count(line => line.Counted && line.Difference == 0),
            session.Lines.Count(line => line.Counted && line.Missing > 0),
            session.Lines.Count(line => line.Counted && line.Surplus > 0),
            session.PendingProducts);

    private static (long? SupplierId, string? Brand, IReadOnlyList<ProductRow> Products) PrepareScope(
        SQLiteConnection connection,
        long businessId,
        InventoryCountSessionInput input)
    {
        return input.Type switch
        {
            InventoryCountType.BySupplier => PrepareSupplierScope(connection, businessId, input.SupplierId),
            InventoryCountType.ByBrand => PrepareBrandScope(connection, businessId, input.Brand),
            InventoryCountType.FreeOperational when input.SupplierId is null && string.IsNullOrWhiteSpace(input.Brand) =>
                (null, null, Array.Empty<ProductRow>()),
            InventoryCountType.FreeOperational =>
                throw new InventoryRuleException("El inventario operativo no usa proveedor ni marca."),
            _ => throw new InventoryRuleException("La modalidad de inventario no es válida.")
        };
    }

    private static (long?, string?, IReadOnlyList<ProductRow>) PrepareSupplierScope(
        SQLiteConnection connection,
        long businessId,
        long? supplierId)
    {
        if (!supplierId.HasValue || SupplierRepository.GetRow(connection, businessId, supplierId.Value) is not { Active: 1 })
        {
            throw new InventoryRuleException("Selecciona un proveedor activo.");
        }

        var products = QueryProductsBySupplier(connection, businessId, supplierId.Value);
        if (products.Count == 0)
        {
            throw new InventoryRuleException("El proveedor seleccionado no tiene productos activos relacionados.");
        }

        return (supplierId, null, products);
    }

    private static (long?, string?, IReadOnlyList<ProductRow>) PrepareBrandScope(
        SQLiteConnection connection,
        long businessId,
        string? brand)
    {
        var displayBrand = InventoryCatalogService.NormalizeBrandDisplay(brand);
        var key = InventoryCatalogService.NormalizeBrandKey(displayBrand);
        if (key.Length == 0)
        {
            throw new InventoryRuleException("Selecciona una marca.");
        }

        var products = QueryAllActiveProducts(connection, businessId)
            .Where(product => InventoryCatalogService.NormalizeBrandKey(product.Brand) == key)
            .ToArray();
        if (products.Length == 0)
        {
            throw new InventoryRuleException("La marca seleccionada no tiene productos activos.");
        }

        return (null, displayBrand, products);
    }

    private static IReadOnlyList<ProductRow> QueryProductsBySupplier(
        SQLiteConnection connection,
        long businessId,
        long supplierId) =>
        connection.Query<ProductRow>(
            $"""
            SELECT DISTINCT {ProductColumns("p")}
            FROM products p
            JOIN product_suppliers ps ON ps.product_id=p.id AND ps.active=1
            JOIN suppliers s ON s.id=ps.supplier_id AND s.active=1
            WHERE p.business_id=? AND p.active=1 AND s.id=?
            ORDER BY p.name COLLATE NOCASE,p.id;
            """,
            businessId,
            supplierId);

    private static IReadOnlyList<ProductRow> QueryAllActiveProducts(SQLiteConnection connection, long businessId) =>
        connection.Query<ProductRow>(
            $"SELECT {ProductColumns("p")} FROM products p WHERE p.business_id=? AND p.active=1 ORDER BY p.name COLLATE NOCASE,p.id;",
            businessId);

    private static string ProductColumns(string alias) => $"""
        {alias}.id Id,{alias}.business_id BusinessId,{alias}.sku Sku,{alias}.barcode Barcode,
        {alias}.name Name,{alias}.description Description,{alias}.brand Brand,
        {alias}.unit_of_measure UnitOfMeasure,{alias}.stock_milli StockMilli,
        {alias}.minimum_stock_milli MinimumStockMilli,{alias}.sale_price_basis SalePriceBasis,
        {alias}.expiration_mode ExpirationMode,NULL NearestExpirationDate,0 UndatedStockMilli,
        {alias}.active Active,{alias}.created_at CreatedAt,{alias}.updated_at UpdatedAt
        """;

    private static void InsertProductLine(SQLiteConnection connection, long countId, ProductRow product) =>
        connection.Execute(
            """
            INSERT INTO inventory_count_lines(
                count_id,product_id,theoretical_milli,physical_milli,difference_milli,
                counted,counted_at,observations,count_by_lot)
            VALUES(?,?,?,NULL,NULL,0,NULL,NULL,0);
            """,
            countId,
            product.Id,
            product.StockMilli);

    private static InventoryCount RequireEditable(SQLiteConnection connection, long businessId, long sessionId)
    {
        var session = GetSession(connection, businessId, sessionId)
            ?? throw new InventoryRuleException("La sesión de inventario no existe.");
        if (!session.IsEditable)
        {
            throw new InventoryRuleException("La sesión ya está completada o cancelada y no puede modificarse.");
        }

        return session;
    }

    private static void ValidatePhysicalQuantity(decimal quantity, UnitOfMeasure unit)
    {
        var normalized = InventoryRules.NormalizeQuantity(quantity);
        if (normalized < 0)
        {
            throw new InventoryRuleException("El inventario físico no puede ser negativo.");
        }

        if (unit == UnitOfMeasure.Unit && normalized != decimal.Truncate(normalized))
        {
            throw new InventoryRuleException("El inventario físico debe ser entero cuando la unidad es pieza.");
        }
    }

    private static void ValidateBeforeConfirmation(
        SQLiteConnection connection,
        long businessId,
        IReadOnlyList<InventoryCountLine> lines)
    {
        foreach (var line in lines)
        {
            var product = ProductRepository.GetRow(connection, businessId, line.ProductId)?.ToDomain()
                ?? throw new InventoryRuleException($"El producto {line.ProductName} ya no existe.");
            if (product.Stock != line.TheoreticalStock)
            {
                throw new InventoryRuleException(
                    $"El stock de {line.ProductName} cambió después de iniciar la sesión. Cancela esta sesión e inicia una nueva.");
            }

            if (line.ExpirationMode == ExpirationMode.Tracked && line.Difference != 0 && !line.CountByLot)
            {
                throw new InventoryRuleException(
                    $"{line.ProductName} controla caducidad. Abre su detalle y captura el conteo por lote antes de confirmar.");
            }

            if (!line.CountByLot)
            {
                continue;
            }

            if (line.LotLines.Count == 0 || line.LotLines.Any(lot => !lot.Counted))
            {
                throw new InventoryRuleException($"Completa el conteo de todos los lotes de {line.ProductName}.");
            }

            foreach (var lot in line.LotLines)
            {
                var currentMilli = connection.ExecuteScalar<long>(
                    "SELECT COALESCE(MAX(quantity_milli),-1) FROM inventory_lots WHERE id=? AND product_id=?;",
                    lot.LotId,
                    line.ProductId);
                if (currentMilli < 0 || SqliteValues.FromMilli(currentMilli) != lot.TheoreticalQuantity)
                {
                    throw new InventoryRuleException(
                        $"El lote {lot.LotCode} de {line.ProductName} cambió después de iniciar el conteo.");
                }
            }
        }
    }

    private static void ApplyProductAdjustment(
        SQLiteConnection connection,
        long businessId,
        InventoryCount session,
        InventoryCountLine line,
        string now)
    {
        var difference = line.Difference;
        if (difference == 0)
        {
            return;
        }

        var allocations = InventoryLotPersistence.ApplyStockChange(
            connection,
            line.ProductId,
            difference,
            session.Reference,
            now,
            allowExpiredLots: true);
        var resulting = line.PhysicalStock!.Value;
        connection.Execute(
            "UPDATE products SET stock_milli=?,updated_at=? WHERE id=?;",
            SqliteValues.ToMilli(resulting),
            now,
            line.ProductId);
        var movementId = ProductRepository.InsertMovement(
            connection,
            businessId,
            line.ProductId,
            difference > 0 ? InventoryMovementType.PositiveAdjustment : InventoryMovementType.NegativeAdjustment,
            difference,
            line.TheoreticalStock,
            resulting,
            session.Reference,
            "Ajuste por conteo físico",
            now,
            session.Id);
        InventoryLotPersistence.RecordMovementAllocations(connection, movementId, allocations);
    }

    private static void ApplyLotAdjustments(
        SQLiteConnection connection,
        long businessId,
        InventoryCount session,
        InventoryCountLine line,
        string now)
    {
        var runningStock = line.TheoreticalStock;
        foreach (var lot in line.LotLines.Where(item => item.Difference != 0))
        {
            var difference = lot.Difference;
            var resultingLot = lot.PhysicalQuantity!.Value;
            connection.Execute(
                """
                UPDATE inventory_lots
                SET quantity_milli=?,status=CASE WHEN ?=0 THEN 1 ELSE 0 END,updated_at=?
                WHERE id=? AND product_id=?;
                """,
                SqliteValues.ToMilli(resultingLot),
                SqliteValues.ToMilli(resultingLot),
                now,
                lot.LotId,
                line.ProductId);
            var resultingStock = InventoryRules.NormalizeQuantity(runningStock + difference);
            var movementId = ProductRepository.InsertMovement(
                connection,
                businessId,
                line.ProductId,
                difference > 0 ? InventoryMovementType.PositiveAdjustment : InventoryMovementType.NegativeAdjustment,
                difference,
                runningStock,
                resultingStock,
                session.Reference,
                $"Ajuste por conteo físico · Lote {lot.LotCode}",
                now,
                session.Id);
            InventoryLotPersistence.RecordMovementAllocations(
                connection,
                movementId,
                [new LotAllocation(lot.LotId, decimal.Abs(difference))]);
            runningStock = resultingStock;
        }

        connection.Execute(
            "UPDATE products SET stock_milli=?,updated_at=? WHERE id=?;",
            SqliteValues.ToMilli(runningStock),
            now,
            line.ProductId);
    }

    private static InventoryCount? GetSession(SQLiteConnection connection, long businessId, long sessionId)
    {
        var row = connection.Query<SessionRow>(
                """
                SELECT c.id Id,c.business_id BusinessId,c.reference Reference,c.inventory_type InventoryType,
                       c.supplier_id SupplierId,s.company_name SupplierName,c.brand Brand,c.status Status,
                       c.notes Notes,c.started_at StartedAt,c.counted_at CountedAt,c.created_at CreatedAt,
                       c.updated_at UpdatedAt,c.confirmed_at ConfirmedAt,c.cancelled_at CancelledAt
                FROM inventory_counts c
                LEFT JOIN suppliers s ON s.id=c.supplier_id
                WHERE c.id=? AND c.business_id=? LIMIT 1;
                """,
                sessionId,
                businessId)
            .FirstOrDefault();
        if (row is null)
        {
            return null;
        }

        var lines = connection.Query<SessionLineRow>(
            """
            SELECT cl.id Id,cl.count_id CountId,cl.product_id ProductId,COALESCE(p.barcode,p.sku) Code,
                   p.sku Sku,p.barcode Barcode,p.name ProductName,p.brand Brand,
                   p.unit_of_measure UnitOfMeasure,p.expiration_mode ExpirationMode,
                   cl.theoretical_milli TheoreticalMilli,cl.physical_milli PhysicalMilli,
                   cl.counted_at CountedAt,cl.observations Observations,cl.count_by_lot CountByLot
            FROM inventory_count_lines cl
            JOIN products p ON p.id=cl.product_id
            WHERE cl.count_id=? ORDER BY p.name COLLATE NOCASE,p.id;
            """,
            row.Id);
        return new InventoryCount
        {
            Id = row.Id,
            BusinessId = row.BusinessId,
            Reference = row.Reference,
            Type = (InventoryCountType)row.InventoryType,
            SupplierId = row.SupplierId,
            SupplierName = row.SupplierName,
            Brand = row.Brand,
            Status = (InventoryCountStatus)row.Status,
            Notes = row.Notes,
            StartedAt = SqliteValues.ParseDate(row.StartedAt),
            CountedAt = SqliteValues.ParseDate(row.CountedAt),
            CreatedAt = SqliteValues.ParseDate(row.CreatedAt),
            UpdatedAt = SqliteValues.ParseDate(row.UpdatedAt),
            ConfirmedAt = row.ConfirmedAt is null ? null : SqliteValues.ParseDate(row.ConfirmedAt),
            CancelledAt = row.CancelledAt is null ? null : SqliteValues.ParseDate(row.CancelledAt),
            Lines = lines.Select(line => new InventoryCountLine
            {
                Id = line.Id,
                CountId = line.CountId,
                ProductId = line.ProductId,
                Code = line.Code,
                Sku = line.Sku,
                Barcode = line.Barcode,
                ProductName = line.ProductName,
                Brand = line.Brand,
                UnitOfMeasure = (UnitOfMeasure)line.UnitOfMeasure,
                ExpirationMode = (ExpirationMode)line.ExpirationMode,
                TheoreticalStock = SqliteValues.FromMilli(line.TheoreticalMilli),
                PhysicalStock = line.PhysicalMilli.HasValue ? SqliteValues.FromMilli(line.PhysicalMilli.Value) : null,
                CountedAt = line.CountedAt is null ? null : SqliteValues.ParseDate(line.CountedAt),
                Observations = line.Observations,
                CountByLot = line.CountByLot == 1,
                LotLines = GetLotLines(connection, line.Id)
            }).ToList()
        };
    }

    private static List<InventoryCountLotLine> GetLotLines(SQLiteConnection connection, long countLineId) =>
        connection.Query<SessionLotLineRow>(
                """
                SELECT cl.id Id,cl.count_line_id CountLineId,cl.lot_id LotId,
                       COALESCE(NULLIF(l.lot_code,''),'Sin código') LotCode,l.expiration_date ExpirationDate,
                       cl.theoretical_milli TheoreticalMilli,cl.physical_milli PhysicalMilli,
                       cl.counted_at CountedAt,cl.observations Observations
                FROM inventory_count_lot_lines cl
                JOIN inventory_lots l ON l.id=cl.lot_id
                WHERE cl.count_line_id=?
                ORDER BY CASE WHEN l.expiration_date IS NULL THEN 1 ELSE 0 END,l.expiration_date,l.received_at,l.id;
                """,
                countLineId)
            .Select(row => new InventoryCountLotLine
            {
                Id = row.Id,
                CountLineId = row.CountLineId,
                LotId = row.LotId,
                LotCode = row.LotCode,
                ExpirationDate = row.ExpirationDate is null
                    ? null
                    : DateOnly.ParseExact(row.ExpirationDate, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                TheoreticalQuantity = SqliteValues.FromMilli(row.TheoreticalMilli),
                PhysicalQuantity = row.PhysicalMilli.HasValue ? SqliteValues.FromMilli(row.PhysicalMilli.Value) : null,
                CountedAt = row.CountedAt is null ? null : SqliteValues.ParseDate(row.CountedAt),
                Observations = row.Observations
            })
            .ToList();

    private static void Touch(SQLiteConnection connection, long sessionId, string? now = null) =>
        connection.Execute(
            "UPDATE inventory_counts SET updated_at=? WHERE id=?;",
            now ?? SqliteValues.Date(DateTime.UtcNow),
            sessionId);

    private sealed class IdRow
    {
        public long Id { get; set; }
    }

    private sealed class SessionRow
    {
        public long Id { get; set; }
        public long BusinessId { get; set; }
        public string Reference { get; set; } = string.Empty;
        public int InventoryType { get; set; }
        public long? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public string? Brand { get; set; }
        public int Status { get; set; }
        public string? Notes { get; set; }
        public string StartedAt { get; set; } = string.Empty;
        public string CountedAt { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
        public string? ConfirmedAt { get; set; }
        public string? CancelledAt { get; set; }
    }

    private sealed class SessionLineRow
    {
        public long Id { get; set; }
        public long CountId { get; set; }
        public long ProductId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public int UnitOfMeasure { get; set; }
        public int ExpirationMode { get; set; }
        public long TheoreticalMilli { get; set; }
        public long? PhysicalMilli { get; set; }
        public string? CountedAt { get; set; }
        public string? Observations { get; set; }
        public int CountByLot { get; set; }
    }

    private sealed class SessionLotLineRow
    {
        public long Id { get; set; }
        public long CountLineId { get; set; }
        public long LotId { get; set; }
        public string LotCode { get; set; } = string.Empty;
        public string? ExpirationDate { get; set; }
        public long TheoreticalMilli { get; set; }
        public long? PhysicalMilli { get; set; }
        public string? CountedAt { get; set; }
        public string? Observations { get; set; }
    }

    private sealed class LotSnapshotRow
    {
        public long Id { get; set; }
        public long QuantityMilli { get; set; }
    }

    private sealed class CountLotOwnerRow
    {
        public long Id { get; set; }
        public long CountLineId { get; set; }
        public long ProductId { get; set; }
        public int UnitOfMeasure { get; set; }
    }
}
