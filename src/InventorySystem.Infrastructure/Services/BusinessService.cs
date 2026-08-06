using InventorySystem.Domain;
using InventorySystem.Infrastructure.Data;

namespace InventorySystem.Infrastructure.Services;

public sealed class BusinessService
{
    private readonly InventoryDatabase _database;

    public BusinessService(InventoryDatabase database)
    {
        _database = database;
    }

    public Task<Business> GetDefaultAsync(CancellationToken cancellationToken = default) =>
        _database.ReadAsync(connection =>
        {
            var row = connection.Query<BusinessRow>(
                    "SELECT id Id,name Name,allow_negative_stock AllowNegativeStock,created_at CreatedAt,updated_at UpdatedAt FROM businesses ORDER BY id LIMIT 1;")
                .Single();
            return ToDomain(row);
        }, cancellationToken);

    public Task<Business> UpdateAsync(
        long businessId,
        string name,
        bool allowNegativeStock,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InventoryRuleException("El nombre del negocio es obligatorio.");
        }

        return _database.WriteAsync(connection =>
        {
            var now = SqliteValues.Date(DateTime.UtcNow);
            var changed = connection.Execute(
                "UPDATE businesses SET name=?,allow_negative_stock=?,updated_at=? WHERE id=?;",
                name.Trim(),
                allowNegativeStock ? 1 : 0,
                now,
                businessId);
            if (changed != 1)
            {
                throw new InventoryRuleException("El negocio no existe.");
            }

            var row = connection.Query<BusinessRow>(
                    "SELECT id Id,name Name,allow_negative_stock AllowNegativeStock,created_at CreatedAt,updated_at UpdatedAt FROM businesses WHERE id=?;",
                    businessId)
                .Single();
            return ToDomain(row);
        }, cancellationToken);
    }

    private static Business ToDomain(BusinessRow row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        AllowNegativeStock = row.AllowNegativeStock == 1,
        CreatedAt = SqliteValues.ParseDate(row.CreatedAt),
        UpdatedAt = SqliteValues.ParseDate(row.UpdatedAt)
    };

    private sealed class BusinessRow
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int AllowNegativeStock { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }
}
