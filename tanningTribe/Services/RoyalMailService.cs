using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using tanningTribe.Models;

namespace tanningTribe.Services;

public record RoyalMailOrderResult(bool Success, string Message, string? OrderIdentifier = null, string? TrackingNumber = null);

public record RoyalMailServiceOption(string Code, string Name, string PackageFormat, decimal Cost);

/// <summary>Creates Royal Mail Click &amp; Drop orders and downloads postage labels.</summary>
public partial class RoyalMailService(HttpClient http, IOptions<RoyalMailSettings> options, ILogger<RoyalMailService> logger)
{
    private readonly RoyalMailSettings _settings = options.Value;

    public bool IsConfigured => _settings.IsConfigured;
    public int DefaultWeightGrams => _settings.DefaultWeightGrams;

    public async Task<RoyalMailOrderResult> CreateOrderAsync(Order order, int? weightGrams = null, string? serviceCode = null, string? packageFormat = null)
    {
        if (!IsConfigured)
        {
            return new(false, "Royal Mail is not configured. Set RoyalMail:ApiKey.");
        }

        var address = ParseAddress(order.ShippingAddress);
        if (string.IsNullOrWhiteSpace(address.Postcode))
        {
            return new(false, "Could not find a postcode in the shipping address. Edit the address so the postcode is on the last line.");
        }

        var payload = new
        {
            items = new[]
            {
                new
                {
                    orderReference = order.OrderNumber,
                    recipient = new
                    {
                        address = new
                        {
                            fullName = order.CustomerName,
                            addressLine1 = address.Line1,
                            addressLine2 = address.Line2,
                            addressLine3 = address.Line3,
                            city = address.City,
                            postcode = address.Postcode,
                            countryCode = "GB"
                        },
                        emailAddress = string.IsNullOrWhiteSpace(order.CustomerEmail) ? null : order.CustomerEmail,
                        phoneNumber = string.IsNullOrWhiteSpace(order.WhatsAppNumber) ? null : order.WhatsAppNumber
                    },
                    orderDate = order.CreatedUtc,
                    subtotal = order.Total,
                    shippingCostCharged = 0m,
                    otherCosts = 0m,
                    total = order.Total,
                    currencyCode = "GBP",
                    packages = new[]
                    {
                        new
                        {
                            weightInGrams = Math.Max(weightGrams ?? _settings.DefaultWeightGrams, 50),
                            packageFormatIdentifier = string.IsNullOrWhiteSpace(packageFormat) ? _settings.PackageFormat : packageFormat,
                            contents = new[]
                            {
                                new
                                {
                                    name = "Health and beauty",
                                    quantity = 1,
                                    unitValue = order.Total,
                                    unitWeightInGrams = 0
                                }
                            }
                        }
                    },
                    postageDetails = new
                    {
                        serviceCode = string.IsNullOrWhiteSpace(serviceCode) ? _settings.DefaultServiceCode : serviceCode
                    }
                }
            }
        };

        try
        {
            using var request = BuildRequest(HttpMethod.Post, "orders");
            request.Content = JsonContent.Create(payload, options: new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
            using var response = await http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Click & Drop create order failed ({Status}): {Body}", (int)response.StatusCode, body);
                return new(false, $"Royal Mail rejected the order ({(int)response.StatusCode}). {Truncate(body)}");
            }

            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;

            if (root.TryGetProperty("failedOrders", out var failed) && failed.ValueKind == JsonValueKind.Array && failed.GetArrayLength() > 0)
            {
                var errors = failed[0].TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array
                    ? string.Join("; ", errs.EnumerateArray().Select(e => e.TryGetProperty("errorMessage", out var m) ? m.GetString() : e.ToString()))
                    : failed[0].ToString();
                return new(false, $"Royal Mail could not create the order: {errors}");
            }

            if (!root.TryGetProperty("createdOrders", out var created) || created.ValueKind != JsonValueKind.Array || created.GetArrayLength() == 0)
            {
                return new(false, "Royal Mail returned no created orders.");
            }

            var first = created[0];
            var identifier = first.GetProperty("orderIdentifier").ToString();
            var tracking = first.TryGetProperty("trackingNumber", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            return new(true, $"Royal Mail order {identifier} created.", identifier, tracking);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Click & Drop create order threw");
            return new(false, $"Could not reach Royal Mail: {ex.Message}");
        }
    }

    /// <summary>Lists Royal Mail online-postage services available for a UK parcel of the given weight, with prices.</summary>
    public async Task<List<RoyalMailServiceOption>> GetServicesAsync(int weightGrams, string? postcode)
    {
        var result = new List<RoyalMailServiceOption>();
        if (!IsConfigured)
        {
            return result;
        }

        try
        {
            var query = $"services/olp?country=GBR&weight={Math.Max(weightGrams, 1)}";
            if (!string.IsNullOrWhiteSpace(postcode))
            {
                query += $"&postcode={Uri.EscapeDataString(postcode.Replace(" ", string.Empty))}";
            }

            using var request = BuildRequest(HttpMethod.Get, query);
            using var response = await http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Click & Drop OLP services lookup failed ({Status}): {Body}", (int)response.StatusCode, body);
                return result;
            }

            using var json = JsonDocument.Parse(body);
            if (!json.RootElement.TryGetProperty("services", out var services) || services.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var s in services.EnumerateArray())
            {
                var code = s.TryGetProperty("serviceCode", out var c) ? c.GetString() : null;
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                var name = s.TryGetProperty("serviceName", out var n) ? n.GetString() ?? code : code;
                var format = s.TryGetProperty("packageFormat", out var f) ? f.GetString() ?? "" : "";
                var cost = s.TryGetProperty("cost", out var co) && co.ValueKind == JsonValueKind.Number ? co.GetDecimal() : 0m;
                result.Add(new RoyalMailServiceOption(code, name, format, cost));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Click & Drop OLP services lookup threw");
        }

        return result.OrderBy(r => r.Cost).ThenBy(r => r.Name).ToList();
    }

    public async Task<byte[]?> GetLabelPdfAsync(string orderIdentifier)
    {
        if (!IsConfigured)
        {
            return null;
        }

        try
        {
            using var request = BuildRequest(HttpMethod.Get, $"orders/{Uri.EscapeDataString(orderIdentifier)}/label?documentType=postageLabel&includeReturnsLabel=false");
            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Click & Drop label download failed ({Status}): {Body}", (int)response.StatusCode, await response.Content.ReadAsStringAsync());
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Click & Drop label download threw");
            return null;
        }
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string relative)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(_settings.BaseUrl), relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pdf"));
        return request;
    }

    /// <summary>Splits a free-text address into Click &amp; Drop fields. Finds the UK postcode wherever it appears, treats the line before it as the town and ignores a trailing country line.</summary>
    internal static (string Line1, string? Line2, string? Line3, string City, string Postcode) ParseAddress(string address)
    {
        var lines = address
            .Replace("\r", "")
            .Split(['\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !IsCountry(l))
            .ToList();

        if (lines.Count == 0)
        {
            return ("", null, null, "", "");
        }

        var postcode = "";
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            var match = UkPostcode().Match(lines[i]);
            if (!match.Success)
            {
                continue;
            }

            postcode = FormatPostcode(match.Value);
            var remainder = lines[i].Remove(match.Index, match.Length).Trim().TrimEnd(',').Trim();
            if (remainder.Length == 0)
            {
                lines.RemoveAt(i);
            }
            else
            {
                lines[i] = remainder;
            }
            break;
        }

        if (postcode.Length == 0)
        {
            return (lines[0], lines.Count > 1 ? lines[1] : null, lines.Count > 2 ? string.Join(", ", lines.Skip(2)) : null, "", "");
        }

        var city = lines.Count > 0 ? lines[^1] : "";
        if (lines.Count > 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var line1 = lines.Count > 0 ? lines[0] : city;
        var line2 = lines.Count > 1 ? lines[1] : null;
        var line3 = lines.Count > 2 ? string.Join(", ", lines.Skip(2)) : null;
        return (line1, line2, line3, city, postcode);
    }

    private static string FormatPostcode(string raw)
    {
        var compact = raw.Replace(" ", string.Empty).ToUpperInvariant();
        return compact.Length > 3 ? $"{compact[..^3]} {compact[^3..]}" : compact;
    }

    private static bool IsCountry(string line)
    {
        var l = line.Trim().ToUpperInvariant().TrimEnd('.');
        return l is "UK" or "U.K" or "GB" or "GREAT BRITAIN" or "UNITED KINGDOM" or "ENGLAND" or "SCOTLAND" or "WALES" or "NORTHERN IRELAND";
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\b(GIR\s?0AA|[A-PR-UWYZ][A-HK-Y]?\d[A-Z\d]?\s*\d[ABD-HJLNP-UW-Z]{2})\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex UkPostcode();

    private static string Truncate(string s) => s.Length <= 300 ? s : s[..300] + "…";
}
