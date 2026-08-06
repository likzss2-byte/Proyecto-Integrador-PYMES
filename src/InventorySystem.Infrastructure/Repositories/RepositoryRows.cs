using InventorySystem.Domain;

namespace InventorySystem.Infrastructure.Repositories;

internal sealed class ProductRow
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public int UnitOfMeasure { get; set; }
    public long StockMilli { get; set; }
    public long MinimumStockMilli { get; set; }
    public long SalePriceBasis { get; set; }
    public int ExpirationMode { get; set; }
    public string? NearestExpirationDate { get; set; }
    public long UndatedStockMilli { get; set; }
    public int Active { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

internal sealed class SupplierRow
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Country { get; set; }
    public string? State { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public int Active { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

internal sealed class ProductSupplierRow
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public long SupplierId { get; set; }
    public string? SupplierSku { get; set; }
    public long? ReferenceCostBasis { get; set; }
    public int Active { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

internal sealed class DocumentRow
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public int DocumentType { get; set; }
    public int Status { get; set; }
    public string Reference { get; set; } = string.Empty;
    public long? SupplierId { get; set; }
    public string? Notes { get; set; }
    public long TotalBasis { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
    public string? ConfirmedAt { get; set; }
    public string? CancelledAt { get; set; }
}

internal sealed class DocumentLineRow
{
    public long Id { get; set; }
    public long DocumentId { get; set; }
    public long ProductId { get; set; }
    public long QuantityMilli { get; set; }
    public long UnitPriceBasis { get; set; }
    public string? LotCode { get; set; }
    public string? ManufacturingDate { get; set; }
    public string? ExpirationDate { get; set; }
}

internal sealed class MovementRow
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int MovementType { get; set; }
    public long QuantityMilli { get; set; }
    public long PreviousStockMilli { get; set; }
    public long ResultingStockMilli { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public long? InventoryCountId { get; set; }
    public string OccurredAt { get; set; } = string.Empty;
}

internal sealed class CountRow
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public int Status { get; set; }
    public string? Notes { get; set; }
    public string CountedAt { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string? ConfirmedAt { get; set; }
}

internal sealed class CountLineRow
{
    public long Id { get; set; }
    public long CountId { get; set; }
    public long ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int UnitOfMeasure { get; set; }
    public long TheoreticalMilli { get; set; }
    public long? PhysicalMilli { get; set; }
}

internal static class RepositoryRowMapper
{
    public const string ProductColumns = """
        id Id,business_id BusinessId,sku Sku,barcode Barcode,name Name,description Description,
        brand Brand,unit_of_measure UnitOfMeasure,stock_milli StockMilli,
        minimum_stock_milli MinimumStockMilli,sale_price_basis SalePriceBasis,expiration_mode ExpirationMode,
        (SELECT MIN(l.expiration_date) FROM inventory_lots l WHERE l.product_id=products.id AND l.quantity_milli>0 AND l.expiration_date IS NOT NULL) NearestExpirationDate,
        COALESCE((SELECT SUM(l.quantity_milli) FROM inventory_lots l WHERE l.product_id=products.id AND l.quantity_milli>0 AND l.expiration_date IS NULL),0) UndatedStockMilli,
        active Active,created_at CreatedAt,updated_at UpdatedAt
        """;

    public const string SupplierColumns = """
        id Id,business_id BusinessId,company_name CompanyName,contact_name ContactName,phone Phone,
        email Email,country Country,state State,address Address,notes Notes,active Active,
        created_at CreatedAt,updated_at UpdatedAt
        """;

    public static Product ToDomain(this ProductRow row) => new()
    {
        Id = row.Id,
        BusinessId = row.BusinessId,
        Sku = row.Sku,
        Barcode = row.Barcode,
        Name = row.Name,
        Description = row.Description,
        Brand = row.Brand,
        UnitOfMeasure = (UnitOfMeasure)row.UnitOfMeasure,
        Stock = Data.SqliteValues.FromMilli(row.StockMilli),
        MinimumStock = Data.SqliteValues.FromMilli(row.MinimumStockMilli),
        SalePrice = Data.SqliteValues.FromMoney(row.SalePriceBasis),
        ExpirationMode = (ExpirationMode)row.ExpirationMode,
        NearestExpirationDate = row.NearestExpirationDate is null
            ? null
            : DateOnly.ParseExact(row.NearestExpirationDate, "yyyy-MM-dd"),
        UndatedStock = Data.SqliteValues.FromMilli(row.UndatedStockMilli),
        Active = row.Active == 1,
        CreatedAt = Data.SqliteValues.ParseDate(row.CreatedAt),
        UpdatedAt = Data.SqliteValues.ParseDate(row.UpdatedAt)
    };

    public static Supplier ToDomain(this SupplierRow row) => new()
    {
        Id = row.Id,
        BusinessId = row.BusinessId,
        CompanyName = row.CompanyName,
        ContactName = row.ContactName,
        Phone = row.Phone,
        Email = row.Email,
        Country = row.Country,
        State = row.State,
        Address = row.Address,
        Notes = row.Notes,
        Active = row.Active == 1,
        CreatedAt = Data.SqliteValues.ParseDate(row.CreatedAt),
        UpdatedAt = Data.SqliteValues.ParseDate(row.UpdatedAt)
    };

    public static ProductSupplier ToDomain(this ProductSupplierRow row) => new()
    {
        Id = row.Id,
        ProductId = row.ProductId,
        SupplierId = row.SupplierId,
        SupplierSku = row.SupplierSku,
        ReferenceCost = row.ReferenceCostBasis.HasValue
            ? Data.SqliteValues.FromMoney(row.ReferenceCostBasis.Value)
            : null,
        Active = row.Active == 1,
        CreatedAt = Data.SqliteValues.ParseDate(row.CreatedAt),
        UpdatedAt = Data.SqliteValues.ParseDate(row.UpdatedAt)
    };
}
