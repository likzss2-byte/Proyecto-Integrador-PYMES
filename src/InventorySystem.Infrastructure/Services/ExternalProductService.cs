using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InventorySystem.Domain;

namespace InventorySystem.Infrastructure.Services;

public sealed class ExternalProductService : IExternalProductCatalog
{
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _timeout;

    public ExternalProductService(HttpClient httpClient, TimeSpan? timeout = null)
    {
        _httpClient = httpClient;
        _timeout = timeout ?? TimeSpan.FromSeconds(8);
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("InventorySystem-MAUI/1.0");
        }
    }

    public async Task<ExternalProduct?> FindAsync(
        string barcode,
        CancellationToken cancellationToken = default)
    {
        barcode = InventoryRules.NormalizeScannedCode(barcode);
        if (!BarcodeRules.IsSupportedExternalBarcode(barcode) || !BarcodeRules.IsChecksumValid(barcode))
        {
            return null;
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout);
        try
        {
            using var response = await _httpClient.GetAsync(
                $"https://world.openfoodfacts.org/api/v2/product/{Uri.EscapeDataString(barcode)}.json",
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<OpenFoodFactsResponse>(
                cancellationToken: timeoutSource.Token).ConfigureAwait(false);
            if (payload?.Status != 1 || payload.Product is null)
            {
                return null;
            }

            var name = First(
                payload.Product.ProductNameEs,
                payload.Product.ProductName,
                payload.Product.GenericNameEs,
                payload.Product.GenericName);
            if (name is null)
            {
                return null;
            }

            return new ExternalProduct(
                barcode,
                name,
                First(payload.Product.Brands),
                First(payload.Product.GenericNameEs, payload.Product.GenericName),
                "Open Food Facts");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static string? First(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private sealed class OpenFoodFactsResponse
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("product")]
        public OpenFoodFactsProduct? Product { get; set; }
    }

    private sealed class OpenFoodFactsProduct
    {
        [JsonPropertyName("product_name")]
        public string? ProductName { get; set; }

        [JsonPropertyName("product_name_es")]
        public string? ProductNameEs { get; set; }

        [JsonPropertyName("generic_name")]
        public string? GenericName { get; set; }

        [JsonPropertyName("generic_name_es")]
        public string? GenericNameEs { get; set; }

        [JsonPropertyName("brands")]
        public string? Brands { get; set; }
    }
}
