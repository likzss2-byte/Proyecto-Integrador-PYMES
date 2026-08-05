using InventorySystem.Domain;
using InventorySystem.Infrastructure.Data;
using InventorySystem.Infrastructure.Repositories;
using InventorySystem.Infrastructure.Services;
using Xunit;

namespace InventorySystem.Tests;

public sealed class InventoryLogicTests
{
    [Fact]
    public async Task Sku_debe_ser_unico()
    {
        await using var context = await TestContext.CreateAsync();
        await context.CreateProductAsync("SKU-001");

        var error = await Assert.ThrowsAsync<InventoryRuleException>(
            () => context.CreateProductAsync(" sku-001 "));

        Assert.Contains("SKU", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Codigo_de_barras_debe_ser_unico_cuando_existe()
    {
        await using var context = await TestContext.CreateAsync();
        await context.CreateProductAsync("SKU-001", barcode: "7501055300075");

        var error = await Assert.ThrowsAsync<InventoryRuleException>(
            () => context.CreateProductAsync("SKU-002", barcode: "7501055300075"));

        Assert.Contains("barras", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Busca_primero_por_codigo_de_barras_exacto()
    {
        await using var context = await TestContext.CreateAsync();
        var expected = await context.CreateProductAsync("SKU-001", barcode: " 7501055300075 ");

        var found = await context.Products.FindByCodeAsync(context.Business.Id, " 7501055300075 ");

        Assert.Equal(expected.Id, found?.Id);
        Assert.Equal("7501055300075", found?.Barcode);
    }

    [Fact]
    public async Task Busca_por_sku_como_compatibilidad()
    {
        await using var context = await TestContext.CreateAsync();
        var expected = await context.CreateProductAsync("sku-mixto-01");

        var found = await context.Products.FindByCodeAsync(context.Business.Id, " SKU-MIXTO-01 ");

        Assert.Equal(expected.Id, found?.Id);
    }

    [Fact]
    public async Task Registra_producto_con_datos_de_dominio()
    {
        await using var context = await TestContext.CreateAsync();

        var product = await context.Products.SaveAsync(
            context.Business.Id,
            new ProductInput(
                "SKU-REG",
                "7501055300075",
                "Producto registrado",
                "Descripción",
                "Marca",
                UnitOfMeasure.Unit,
                2,
                19.95m),
            5);

        Assert.True(product.Id > 0);
        Assert.Equal(5, product.Stock);
        Assert.Equal(2, product.MinimumStock);
        Assert.Equal(19.95m, product.SalePrice);
        Assert.Single(await context.Adjustments.GetMovementsAsync(context.Business.Id, product.Id));
    }

    [Fact]
    public async Task Producto_inactivo_se_conserva_pero_no_participa_en_operaciones()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-INACTIVO", stock: 3);
        await context.Products.SaveAsync(
            context.Business.Id,
            new ProductInput(product.Sku, product.Barcode, product.Name, null, null, product.UnitOfMeasure, 0, 0, false),
            productId: product.Id);

        Assert.Empty(await context.Products.SearchAsync(context.Business.Id));
        Assert.Single(await context.Products.SearchAsync(context.Business.Id, includeInactive: true));
        var error = await Assert.ThrowsAsync<InventoryRuleException>(() =>
            context.Transactions.CreateSaleAsync(
                context.Business.Id,
                [new InventoryDocumentLineInput(product.Id, 1, 1)]));
        Assert.Contains("inactivo", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Entrada_admite_varios_productos()
    {
        await using var context = await TestContext.CreateAsync();
        var first = await context.CreateProductAsync("SKU-E1");
        var second = await context.CreateProductAsync("SKU-E2");

        var entry = await context.Transactions.CreateEntryAsync(
            context.Business.Id,
            [new(first.Id, 2, 10), new(second.Id, 3, 5.5m)]);

        Assert.Equal(2, entry.Lines.Count);
        Assert.Equal(36.5m, entry.Total);
        Assert.Equal(InventoryDocumentStatus.Draft, entry.Status);
    }

    [Fact]
    public async Task Entrada_confirmada_aumenta_stock_una_sola_vez()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-E", stock: 1);
        var entry = await context.Transactions.CreateEntryAsync(
            context.Business.Id,
            [new(product.Id, 4, 2)]);

        await context.Transactions.ConfirmAsync(context.Business.Id, entry.Id);
        await Assert.ThrowsAsync<InventoryRuleException>(
            () => context.Transactions.ConfirmAsync(context.Business.Id, entry.Id));

        Assert.Equal(5, (await context.Products.GetAsync(context.Business.Id, product.Id))!.Stock);
    }

    [Fact]
    public async Task Cancelacion_de_entrada_revierte_stock_una_sola_vez()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-CE", stock: 6);
        var entry = await context.Transactions.CreateEntryAsync(context.Business.Id, [new(product.Id, 2, 1)]);
        await context.Transactions.ConfirmAsync(context.Business.Id, entry.Id);

        await context.Transactions.CancelAsync(context.Business.Id, entry.Id, "Entrega devuelta");
        await Assert.ThrowsAsync<InventoryRuleException>(
            () => context.Transactions.CancelAsync(context.Business.Id, entry.Id, "Repetida"));

        Assert.Equal(6, (await context.Products.GetAsync(context.Business.Id, product.Id))!.Stock);
    }

    [Fact]
    public async Task Venta_admite_varios_productos()
    {
        await using var context = await TestContext.CreateAsync();
        var first = await context.CreateProductAsync("SKU-V1", stock: 10);
        var second = await context.CreateProductAsync("SKU-V2", stock: 10);
        var sale = await context.Transactions.CreateSaleAsync(
            context.Business.Id,
            [new(first.Id, 2, 3), new(second.Id, 1, 4)]);

        await context.Transactions.ConfirmAsync(context.Business.Id, sale.Id);

        Assert.Equal(8, (await context.Products.GetAsync(context.Business.Id, first.Id))!.Stock);
        Assert.Equal(9, (await context.Products.GetAsync(context.Business.Id, second.Id))!.Stock);
    }

    [Fact]
    public async Task Venta_rechaza_stock_insuficiente()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-SIN", stock: 1);
        var sale = await context.Transactions.CreateSaleAsync(context.Business.Id, [new(product.Id, 2, 1)]);

        var error = await Assert.ThrowsAsync<InventoryRuleException>(
            () => context.Transactions.ConfirmAsync(context.Business.Id, sale.Id));

        Assert.Contains("insuficiente", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, (await context.Products.GetAsync(context.Business.Id, product.Id))!.Stock);
    }

    [Fact]
    public async Task Venta_confirmada_descuenta_una_sola_vez()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-V", stock: 8);
        var sale = await context.Transactions.CreateSaleAsync(context.Business.Id, [new(product.Id, 3, 1)]);

        await context.Transactions.ConfirmAsync(context.Business.Id, sale.Id);
        await Assert.ThrowsAsync<InventoryRuleException>(
            () => context.Transactions.ConfirmAsync(context.Business.Id, sale.Id));

        Assert.Equal(5, (await context.Products.GetAsync(context.Business.Id, product.Id))!.Stock);
    }

    [Fact]
    public async Task Cancelacion_de_venta_repone_stock_una_sola_vez()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-CV", stock: 8);
        var sale = await context.Transactions.CreateSaleAsync(context.Business.Id, [new(product.Id, 3, 1)]);
        await context.Transactions.ConfirmAsync(context.Business.Id, sale.Id);

        await context.Transactions.CancelAsync(context.Business.Id, sale.Id, "Cliente devolvió el producto");
        await Assert.ThrowsAsync<InventoryRuleException>(
            () => context.Transactions.CancelAsync(context.Business.Id, sale.Id, "Repetida"));

        Assert.Equal(8, (await context.Products.GetAsync(context.Business.Id, product.Id))!.Stock);
    }

    [Fact]
    public async Task Ajuste_exige_motivo()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-A", stock: 2);

        await Assert.ThrowsAsync<InventoryRuleException>(() =>
            context.Adjustments.ApplyAdjustmentAsync(
                context.Business.Id,
                new InventoryAdjustmentInput(product.Id, 1, " ")));

        Assert.Equal(2, (await context.Products.GetAsync(context.Business.Id, product.Id))!.Stock);
    }

    [Fact]
    public async Task Conteo_fisico_calcula_faltante_sin_ajustar_antes_de_confirmar()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-F", stock: 10);

        var count = await context.Adjustments.CreateCountAsync(
            context.Business.Id,
            [new InventoryCountLineInput(product.Id, 7)]);

        Assert.Equal(3, Assert.Single(count.Lines).Missing);
        Assert.Equal(10, (await context.Products.GetAsync(context.Business.Id, product.Id))!.Stock);
        await context.Adjustments.ConfirmCountAsync(context.Business.Id, count.Id);
        Assert.Equal(7, (await context.Products.GetAsync(context.Business.Id, product.Id))!.Stock);
    }

    [Fact]
    public async Task Conteo_fisico_calcula_sobrante()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-S", stock: 4);

        var count = await context.Adjustments.CreateCountAsync(
            context.Business.Id,
            [new InventoryCountLineInput(product.Id, 6)]);

        Assert.Equal(2, Assert.Single(count.Lines).Surplus);
        await context.Adjustments.ConfirmCountAsync(context.Business.Id, count.Id);
        Assert.Equal(6, (await context.Products.GetAsync(context.Business.Id, product.Id))!.Stock);
    }

    [Fact]
    public async Task Kilogramos_y_litros_aceptan_cantidades_decimales()
    {
        await using var context = await TestContext.CreateAsync();
        var kilograms = await context.CreateProductAsync("SKU-KG", stock: 1.125m, unit: UnitOfMeasure.Kilogram);
        var liters = await context.CreateProductAsync("SKU-L", stock: 2.5m, unit: UnitOfMeasure.Liter);
        var entry = await context.Transactions.CreateEntryAsync(
            context.Business.Id,
            [new(kilograms.Id, 0.375m, 1), new(liters.Id, 0.125m, 1)]);

        await context.Transactions.ConfirmAsync(context.Business.Id, entry.Id);

        Assert.Equal(1.5m, (await context.Products.GetAsync(context.Business.Id, kilograms.Id))!.Stock);
        Assert.Equal(2.625m, (await context.Products.GetAsync(context.Business.Id, liters.Id))!.Stock);
    }

    [Fact]
    public async Task Relaciona_producto_con_proveedor()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-R");
        var supplier = await context.Suppliers.SaveAsync(
            context.Business.Id,
            new SupplierInput("Proveedor", "Contacto", "555", "correo@ejemplo.com", "México", "CDMX", "Dirección", null));

        var relation = await context.Suppliers.LinkProductAsync(
            context.Business.Id,
            new ProductSupplierInput(product.Id, supplier.Id, "EXT-123", 12.3456m));

        Assert.Equal("EXT-123", relation.SupplierSku);
        Assert.Equal(12.3456m, relation.ReferenceCost);
        Assert.Single(await context.Suppliers.GetProductSuppliersAsync(context.Business.Id, product.Id));
    }

    [Fact]
    public async Task Transaccion_multiarticulo_es_atomica()
    {
        await using var context = await TestContext.CreateAsync();
        var first = await context.CreateProductAsync("SKU-T1", stock: 5);
        var second = await context.CreateProductAsync("SKU-T2", stock: 5);
        var sale = await context.Transactions.CreateSaleAsync(
            context.Business.Id,
            [new(first.Id, 2, 1), new(second.Id, 3, 1)]);

        await context.Transactions.ConfirmAsync(context.Business.Id, sale.Id);

        Assert.Equal(3, (await context.Products.GetAsync(context.Business.Id, first.Id))!.Stock);
        Assert.Equal(2, (await context.Products.GetAsync(context.Business.Id, second.Id))!.Stock);
        Assert.Equal(4, (await context.Adjustments.GetMovementsAsync(context.Business.Id)).Count);
    }

    [Fact]
    public async Task Error_no_deja_stock_parcialmente_modificado()
    {
        await using var context = await TestContext.CreateAsync();
        var first = await context.CreateProductAsync("SKU-X1", stock: 5);
        var second = await context.CreateProductAsync("SKU-X2", stock: 5);
        var sale = await context.Transactions.CreateSaleAsync(
            context.Business.Id,
            [new(first.Id, 2, 1), new(second.Id, 2, 1)]);
        await context.Products.SaveAsync(
            context.Business.Id,
            new ProductInput(second.Sku, null, second.Name, null, null, UnitOfMeasure.Unit, 0, 0, false),
            productId: second.Id);

        await Assert.ThrowsAsync<InventoryRuleException>(
            () => context.Transactions.ConfirmAsync(context.Business.Id, sale.Id));

        Assert.Equal(5, (await context.Products.GetAsync(context.Business.Id, first.Id))!.Stock);
        Assert.Equal(5, (await context.Products.GetAsync(context.Business.Id, second.Id))!.Stock);
    }

    [Fact]
    public async Task Codigo_desconocido_no_se_guarda_automaticamente()
    {
        await using var context = await TestContext.CreateAsync(new FakeExternalCatalog(
            new ExternalProduct("7501055300075", "Detectado", "Marca", null, "Prueba")));

        var result = await context.Lookup.LookupAsync(context.Business.Id, "7501055300075");

        Assert.True(result.RequiresConfirmation);
        Assert.Empty(await context.Products.SearchAsync(context.Business.Id, includeInactive: true));
    }

    [Fact]
    public void Lecturas_repetidas_no_duplican_operacion()
    {
        var time = new ManualTimeProvider();
        var guard = new BarcodeReadGuard(TimeSpan.FromSeconds(2), time);

        Assert.True(guard.TryAccept("venta", " 7501055300075 ", out var first));
        Assert.False(guard.TryAccept("venta", "7501055300075", out _));
        time.Advance(TimeSpan.FromSeconds(3));
        Assert.True(guard.TryAccept("venta", "7501055300075", out var third));
        Assert.Equal(first, third);
    }

    [Fact]
    public async Task Migracion_respalda_e_importa_esquema_destino_sin_borrarlo()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"inventory-legacy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "InventorySystem.db");
        try
        {
            using (var legacy = new SQLite.SQLiteConnection(databasePath))
            {
                legacy.Execute(
                    """
                    CREATE TABLE Item(
                        ItemID INTEGER PRIMARY KEY AUTOINCREMENT,
                        ItemName TEXT NOT NULL UNIQUE,
                        ItemDescription TEXT NULL,
                        SalePrice REAL NOT NULL,
                        Stock INTEGER NOT NULL,
                        ModifiedAt TEXT NOT NULL
                    );
                    """);
                legacy.Execute(
                    "INSERT INTO Item(ItemName,ItemDescription,SalePrice,Stock,ModifiedAt) VALUES(?,?,?,?,?);",
                    "Producto anterior",
                    "Se conserva",
                    15.5m,
                    7,
                    DateTime.UtcNow.ToString("O"));
            }

            var database = new InventoryDatabase(databasePath);
            await database.InitializeAsync();
            var businesses = new BusinessService(database);
            var products = new ProductRepository(database);
            var business = await businesses.GetDefaultAsync();
            var imported = Assert.Single(await products.SearchAsync(business.Id));

            Assert.Equal("Producto anterior", imported.Name);
            Assert.Equal(7, imported.Stock);
            Assert.NotNull(database.LastBackupPath);
            Assert.True(File.Exists(database.LastBackupPath));
            Assert.True(await database.ReadAsync(connection => connection.GetTableInfo("Item").Count > 0));
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Movimientos_no_se_pueden_editar_ni_eliminar()
    {
        await using var context = await TestContext.CreateAsync();
        await context.CreateProductAsync("SKU-AUD", stock: 2);

        await Assert.ThrowsAsync<SQLite.SQLiteException>(() =>
            context.Database.WriteAsync(connection =>
                connection.Execute("UPDATE inventory_movements SET reason='alterado';")));
        await Assert.ThrowsAsync<SQLite.SQLiteException>(() =>
            context.Database.WriteAsync(connection =>
                connection.Execute("DELETE FROM inventory_movements;")));

        Assert.Single(await context.Adjustments.GetMovementsAsync(context.Business.Id));
    }

    [Fact]
    public async Task Existencia_inicial_se_clasifica_y_genera_alerta_de_caducidad()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-CAD", stock: 3);
        Assert.Equal(3, product.UndatedStock);

        var expiration = DateOnly.FromDateTime(DateTime.Today).AddDays(5);
        product = await context.Lots.ClassifyUndatedStockAsync(
            context.Business.Id,
            product.Id,
            ExpirationMode.Tracked,
            expiration,
            "LOTE-INICIAL");
        var alert = Assert.Single(await context.Lots.GetAlertsAsync(context.Business.Id));

        Assert.Equal(0, product.UndatedStock);
        Assert.Equal(expiration, product.NearestExpirationDate);
        Assert.Equal("LOTE-INICIAL", alert.LotCode);
    }

    [Fact]
    public async Task Salida_consume_primero_el_lote_que_caduca_FEFO()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-FEFO");
        var today = DateOnly.FromDateTime(DateTime.Today);
        await context.Lots.ReceiveAsync(context.Business.Id, product.Id, 5, ExpirationMode.Tracked, today.AddDays(5), "PRIMERO");
        await context.Lots.ReceiveAsync(context.Business.Id, product.Id, 5, ExpirationMode.Tracked, today.AddDays(30), "DESPUES");
        var sale = await context.Transactions.CreateSaleAsync(context.Business.Id, [new(product.Id, 4, 1)]);

        await context.Transactions.ConfirmAsync(context.Business.Id, sale.Id);
        var lots = await context.Lots.GetLotsAsync(context.Business.Id, product.Id);

        Assert.Equal(2, lots.Count);
        Assert.Equal("PRIMERO", lots[0].LotCode);
        Assert.Equal(1, lots[0].Quantity);
        Assert.Equal(5, lots[1].Quantity);
    }

    [Fact]
    public async Task Producto_no_perecedero_no_genera_alertas()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-NP", stock: 2);
        await context.Lots.ClassifyUndatedStockAsync(
            context.Business.Id,
            product.Id,
            ExpirationMode.NotApplicable);

        var summary = await context.Lots.GetSummaryAsync(context.Business.Id);

        Assert.Empty(await context.Lots.GetAlertsAsync(context.Business.Id));
        Assert.Equal(0, summary.NeedsSetupProducts);
        Assert.Equal(0, summary.MissingDateProducts);
    }

    [Fact]
    public async Task Producto_con_caducidad_crea_lote_inicial_y_producto_sin_caducidad_no_exige_fecha()
    {
        await using var context = await TestContext.CreateAsync();
        var expiration = DateOnly.FromDateTime(DateTime.Today).AddDays(15);
        var tracked = await context.CreateProductAsync(
            "SKU-PER",
            stock: 2,
            expirationMode: ExpirationMode.Tracked,
            initialExpirationDate: expiration);
        var durable = await context.CreateProductAsync(
            "SKU-DUR",
            stock: 3,
            expirationMode: ExpirationMode.NotApplicable);

        var trackedLot = Assert.Single(await context.Lots.GetLotsAsync(context.Business.Id, tracked.Id));
        var durableLot = Assert.Single(await context.Lots.GetLotsAsync(context.Business.Id, durable.Id));
        Assert.Equal(expiration, trackedLot.ExpirationDate);
        Assert.Null(durableLot.ExpirationDate);
    }

    [Fact]
    public async Task Dos_lotes_del_mismo_producto_se_conservan_incluso_si_uno_se_agota()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-LOTES", expirationMode: ExpirationMode.Tracked);
        var today = DateOnly.FromDateTime(DateTime.Today);
        await context.Lots.ReceiveAsync(context.Business.Id, product.Id, 2, ExpirationMode.Tracked, today.AddDays(2), "L-A");
        await context.Lots.ReceiveAsync(context.Business.Id, product.Id, 4, ExpirationMode.Tracked, today.AddDays(9), "L-B");
        var sale = await context.Transactions.CreateSaleAsync(context.Business.Id, [new(product.Id, 2, 1)]);

        await context.Transactions.ConfirmAsync(context.Business.Id, sale.Id);

        var lots = await context.Lots.GetLotsAsync(context.Business.Id, product.Id);
        Assert.Equal(2, lots.Count);
        Assert.Equal(0, lots.Single(lot => lot.LotCode == "L-A").Quantity);
        Assert.Equal(InventoryLotStatus.Exhausted, lots.Single(lot => lot.LotCode == "L-A").Status);
        Assert.Equal(4, lots.Single(lot => lot.LotCode == "L-B").Quantity);
    }

    [Fact]
    public async Task Alertas_de_siete_dias_excluyen_fechas_posteriores_y_separan_caducados()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-ALERT", expirationMode: ExpirationMode.Tracked);
        var today = DateOnly.FromDateTime(DateTime.Today);
        await context.Lots.ReceiveAsync(context.Business.Id, product.Id, 1, ExpirationMode.Tracked, today.AddDays(-1), "VENCIDO");
        await context.Lots.ReceiveAsync(context.Business.Id, product.Id, 1, ExpirationMode.Tracked, today, "HOY");
        await context.Lots.ReceiveAsync(context.Business.Id, product.Id, 1, ExpirationMode.Tracked, today.AddDays(7), "DIA-7");
        await context.Lots.ReceiveAsync(context.Business.Id, product.Id, 1, ExpirationMode.Tracked, today.AddDays(8), "DIA-8");

        var expiring = await context.Lots.GetExpiringAsync(context.Business.Id);
        var expired = await context.Lots.GetExpiredAsync(context.Business.Id);

        Assert.Equal(["HOY", "DIA-7"], expiring.Select(alert => alert.LotCode));
        Assert.Equal("VENCIDO", Assert.Single(expired).LotCode);
        Assert.DoesNotContain(expiring, alert => alert.LotCode == "DIA-8");
    }

    [Fact]
    public async Task Venta_bloquea_lote_caducado_sin_confirmacion_explicita()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-VENC", expirationMode: ExpirationMode.Tracked);
        await context.Lots.ReceiveAsync(
            context.Business.Id,
            product.Id,
            2,
            ExpirationMode.Tracked,
            DateOnly.FromDateTime(DateTime.Today).AddDays(-1),
            "CAD");
        var sale = await context.Transactions.CreateSaleAsync(context.Business.Id, [new(product.Id, 1, 1)]);

        await Assert.ThrowsAsync<InventoryRuleException>(
            () => context.Transactions.ConfirmAsync(context.Business.Id, sale.Id));
        Assert.Equal(2, (await context.Products.GetAsync(context.Business.Id, product.Id))!.Stock);
    }

    [Fact]
    public async Task Pedido_manual_sin_codigo_fecha_estimada_ni_producto_no_modifica_inventario()
    {
        await using var context = await TestContext.CreateAsync();
        var existing = await context.CreateProductAsync("SKU-STABLE", stock: 5);

        var order = await context.Orders.CreateAsync(
            context.Business.Id,
            new PurchaseOrderInput(
                null,
                "Proveedor del mercado",
                DateOnly.FromDateTime(DateTime.Today),
                null,
                "Pedido verbal",
                [new(null, "Queso artesanal", null, null, 1.5m, UnitOfMeasure.Kilogram)]));

        Assert.Equal(PurchaseOrderStatus.Pending, order.Status);
        Assert.Null(order.EstimatedDate);
        Assert.Null(order.Lines.Single().ProductId);
        Assert.Null(order.Lines.Single().Barcode);
        Assert.Equal(5, (await context.Products.GetAsync(context.Business.Id, existing.Id))!.Stock);
    }

    [Fact]
    public async Task Recepcion_parcial_calcula_pendiente_y_al_completar_cambia_estado()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync(
            "SKU-REC",
            expirationMode: ExpirationMode.NotApplicable);
        var order = await context.Orders.CreateAsync(
            context.Business.Id,
            new PurchaseOrderInput(
                null,
                "Proveedor manual",
                DateOnly.FromDateTime(DateTime.Today),
                null,
                null,
                [new(product.Id, null, null, null, 10, UnitOfMeasure.Unit, 2m)]));

        var first = await context.Orders.ReceiveAsync(
            context.Business.Id,
            order.Id,
            [new(order.Lines.Single().Id, product.Id, 4, "REC-A")],
            "receipt-partial-1");
        order = (await context.Orders.GetAsync(context.Business.Id, order.Id))!;

        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);
        Assert.Equal(6, order.Lines.Single().PendingQuantity);
        Assert.Equal(4, (await context.Products.GetAsync(context.Business.Id, product.Id))!.Stock);
        Assert.Single(first.Lines);

        await context.Orders.ReceiveAsync(
            context.Business.Id,
            order.Id,
            [new(order.Lines.Single().Id, product.Id, 6, "REC-B")],
            "receipt-partial-2");
        order = (await context.Orders.GetAsync(context.Business.Id, order.Id))!;
        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        Assert.Equal(0, order.Lines.Single().PendingQuantity);
        Assert.Equal(10, (await context.Products.GetAsync(context.Business.Id, product.Id))!.Stock);
    }

    [Fact]
    public async Task Recepcion_crea_lote_con_caducidad_y_movimiento()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-REC-CAD", expirationMode: ExpirationMode.Tracked);
        var supplier = await context.CreateSupplierAsync("Proveedor formal");
        var expiration = DateOnly.FromDateTime(DateTime.Today).AddDays(20);
        var order = await context.Orders.CreateAsync(
            context.Business.Id,
            new PurchaseOrderInput(
                supplier.Id,
                null,
                DateOnly.FromDateTime(DateTime.Today),
                null,
                null,
                [new(product.Id, null, null, null, 3, UnitOfMeasure.Unit)]));

        var receipt = await context.Orders.ReceiveAsync(
            context.Business.Id,
            order.Id,
            [new(order.Lines.Single().Id, product.Id, 3, "LOTE-REC", ExpirationDate: expiration)],
            "receipt-expiration");

        var lot = Assert.Single(await context.Lots.GetLotsAsync(context.Business.Id, product.Id));
        var movement = Assert.Single(
            await context.Adjustments.GetMovementsAsync(context.Business.Id, product.Id),
            item => item.Type == InventoryMovementType.PurchaseReceipt);
        Assert.Equal(receipt.Id, lot.ReceiptId);
        Assert.Equal(order.Id, lot.PurchaseOrderId);
        Assert.Equal(expiration, lot.ExpirationDate);
        Assert.Equal("LOTE-REC", lot.LotCode);
        Assert.Equal(3, movement.ResultingStock);
    }

    [Fact]
    public async Task Recepcion_no_acepta_mas_de_lo_pendiente()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-OVER", expirationMode: ExpirationMode.NotApplicable);
        var order = await context.Orders.CreateAsync(
            context.Business.Id,
            new PurchaseOrderInput(null, "Proveedor", DateOnly.FromDateTime(DateTime.Today), null, null,
                [new(product.Id, null, null, null, 2, UnitOfMeasure.Unit)]));

        await Assert.ThrowsAsync<InventoryRuleException>(() => context.Orders.ReceiveAsync(
            context.Business.Id,
            order.Id,
            [new(order.Lines.Single().Id, product.Id, 3)],
            "receipt-over"));
        Assert.Equal(0, (await context.Products.GetAsync(context.Business.Id, product.Id))!.Stock);
    }

    [Fact]
    public async Task Doble_confirmacion_de_recepcion_es_idempotente()
    {
        await using var context = await TestContext.CreateAsync();
        var product = await context.CreateProductAsync("SKU-IDEMP", expirationMode: ExpirationMode.NotApplicable);
        var order = await context.Orders.CreateAsync(
            context.Business.Id,
            new PurchaseOrderInput(null, "Proveedor", DateOnly.FromDateTime(DateTime.Today), null, null,
                [new(product.Id, null, null, null, 2, UnitOfMeasure.Unit)]));
        var input = new[] { new PurchaseReceiptInput(order.Lines.Single().Id, product.Id, 2) };

        var first = await context.Orders.ReceiveAsync(context.Business.Id, order.Id, input, "same-operation-key");
        var second = await context.Orders.ReceiveAsync(context.Business.Id, order.Id, input, "same-operation-key");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(2, (await context.Products.GetAsync(context.Business.Id, product.Id))!.Stock);
    }

    [Fact]
    public async Task Recepcion_multiarticulo_revierte_todo_si_un_detalle_falla()
    {
        await using var context = await TestContext.CreateAsync();
        var first = await context.CreateProductAsync("SKU-AT-A", expirationMode: ExpirationMode.NotApplicable);
        var second = await context.CreateProductAsync("SKU-AT-B", expirationMode: ExpirationMode.NotApplicable);
        var order = await context.Orders.CreateAsync(
            context.Business.Id,
            new PurchaseOrderInput(null, "Proveedor", DateOnly.FromDateTime(DateTime.Today), null, null,
                [
                    new(first.Id, null, null, null, 2, UnitOfMeasure.Unit),
                    new(second.Id, null, null, null, 2, UnitOfMeasure.Unit)
                ]));

        await Assert.ThrowsAsync<InventoryRuleException>(() => context.Orders.ReceiveAsync(
            context.Business.Id,
            order.Id,
            [
                new(order.Lines[0].Id, first.Id, 2),
                new(order.Lines[1].Id, second.Id, 3)
            ],
            "receipt-atomic"));

        Assert.Equal(0, (await context.Products.GetAsync(context.Business.Id, first.Id))!.Stock);
        Assert.Equal(0, (await context.Products.GetAsync(context.Business.Id, second.Id))!.Stock);
        Assert.All((await context.Orders.GetAsync(context.Business.Id, order.Id))!.Lines, line => Assert.Equal(0, line.ReceivedQuantity));
    }

    [Fact]
    public async Task Panel_muestra_stock_minimo_y_contadores_de_pedidos_reales()
    {
        await using var context = await TestContext.CreateAsync();
        await context.Products.SaveAsync(
            context.Business.Id,
            new ProductInput("SKU-MIN", null, "Producto mínimo", null, null, UnitOfMeasure.Unit, 5, 0),
            2);
        await context.Orders.CreateAsync(
            context.Business.Id,
            new PurchaseOrderInput(null, "Proveedor", DateOnly.FromDateTime(DateTime.Today), null, null,
                [new(null, "Concepto manual", null, null, 1, UnitOfMeasure.Unit)]));

        var dashboard = await context.Dashboard.GetAsync(context.Business.Id);

        Assert.Contains(dashboard.MinimumStock, product => product.Code == "SKU-MIN");
        Assert.Equal(1, dashboard.Summary.PendingOrders);
    }

    private sealed class FakeExternalCatalog(ExternalProduct? result) : IExternalProductCatalog
    {
        public Task<ExternalProduct?> FindAsync(string barcode, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}

internal sealed class TestContext : IAsyncDisposable
{
    private readonly string _directory;

    private TestContext(string directory, InventoryDatabase database, IExternalProductCatalog externalCatalog)
    {
        _directory = directory;
        Database = database;
        Products = new ProductRepository(database);
        Suppliers = new SupplierRepository(database);
        Transactions = new InventoryTransactionService(database);
        Adjustments = new InventoryAdjustmentService(database);
        Lots = new InventoryLotService(database);
        Orders = new PurchaseOrderService(database);
        Dashboard = new DashboardService(database, Lots);
        Businesses = new BusinessService(database);
        Lookup = new ProductLookupService(database, Products, externalCatalog);
    }

    public InventoryDatabase Database { get; }
    public ProductRepository Products { get; }
    public SupplierRepository Suppliers { get; }
    public InventoryTransactionService Transactions { get; }
    public InventoryAdjustmentService Adjustments { get; }
    public InventoryLotService Lots { get; }
    public PurchaseOrderService Orders { get; }
    public DashboardService Dashboard { get; }
    public BusinessService Businesses { get; }
    public ProductLookupService Lookup { get; }
    public Business Business { get; private set; } = null!;

    public static async Task<TestContext> CreateAsync(IExternalProductCatalog? externalCatalog = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"inventory-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var database = new InventoryDatabase(Path.Combine(directory, "inventory.db"));
        var context = new TestContext(directory, database, externalCatalog ?? new FakeEmptyCatalog());
        await database.InitializeAsync();
        context.Business = await context.Businesses.GetDefaultAsync();
        return context;
    }

    public Task<Product> CreateProductAsync(
        string sku,
        decimal stock = 0m,
        UnitOfMeasure unit = UnitOfMeasure.Unit,
        string? barcode = null,
        ExpirationMode expirationMode = ExpirationMode.Unknown,
        DateOnly? initialExpirationDate = null) =>
        Products.SaveAsync(
            Business.Id,
            new ProductInput(
                sku,
                barcode,
                $"Producto {sku}",
                null,
                null,
                unit,
                0,
                0,
                ExpirationMode: expirationMode,
                InitialExpirationDate: initialExpirationDate),
            stock);

    public Task<Supplier> CreateSupplierAsync(string company) =>
        Suppliers.SaveAsync(
            Business.Id,
            new SupplierInput(company, null, null, null, null, null, null, null));

    public ValueTask DisposeAsync()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch
        {
            // La limpieza de archivos temporales no debe ocultar el resultado de una prueba.
        }

        return ValueTask.CompletedTask;
    }

    private sealed class FakeEmptyCatalog : IExternalProductCatalog
    {
        public Task<ExternalProduct?> FindAsync(string barcode, CancellationToken cancellationToken = default) =>
            Task.FromResult<ExternalProduct?>(null);
    }
}
