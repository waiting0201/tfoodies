using System.Text.Json.Serialization;

namespace TFoodies.Contracts.Common;

public class PaginatedResponse<T>
{
    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    public static PaginatedResponse<T> Create(List<T> items, int totalCount, int page, int pageSize)
        => new()
        {
            Items = items,
            Total = totalCount,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 1
        };
}
