using System.Text.Json.Serialization;

namespace KSeX;

public sealed record KSeXAuthConfig
{
    public required string ContextNip { get; init; }
    public string? KsefToken { get; init; }
    public string? RefreshToken { get; init; }
}

public sealed record KSeXAuthResult
{
    public required string AccessToken { get; init; }
    public required DateTimeOffset ValidUntil { get; init; }
    public string? RefreshTokenToSave { get; init; }
}


public sealed record AuthenticationChallengeResponse
{
    [JsonPropertyName("challenge")] public required string Challenge { get; init; }
    [JsonPropertyName("timestampMs")] public long TimestampMs { get; init; }
}

public sealed record PublicKeyCertificate
{
    [JsonPropertyName("certificate")] public required string CertificateBase64 { get; init; }
    [JsonPropertyName("validFrom")] public DateTimeOffset ValidFrom { get; init; }
    [JsonPropertyName("validTo")] public DateTimeOffset ValidTo { get; init; }
    [JsonPropertyName("usage")] public List<string> Usage { get; init; } = new();
}

public sealed record ContextIdentifier
{
    [JsonPropertyName("type")] public required string Type { get; init; }
    [JsonPropertyName("value")] public required string Value { get; init; }
}

public sealed record InitTokenAuthenticationRequest
{
    [JsonPropertyName("challenge")] public required string Challenge { get; init; }
    [JsonPropertyName("contextIdentifier")] public required ContextIdentifier ContextIdentifier { get; init; }
    [JsonPropertyName("encryptedToken")] public required string EncryptedToken { get; init; }
}


public sealed record AuthenticationInitResponse
{
    [JsonPropertyName("referenceNumber")] public required string ReferenceNumber { get; init; }
    [JsonPropertyName("authenticationToken")] public required TokenInfo AuthenticationToken { get; init; }
}

public sealed record TokenInfo
{
    [JsonPropertyName("token")] public required string Token { get; init; }
    [JsonPropertyName("validUntil")] public DateTimeOffset ValidUntil { get; init; }
}

public sealed record AuthenticationTokensResponse
{
    [JsonPropertyName("accessToken")] public required TokenInfo AccessToken { get; init; }
    [JsonPropertyName("refreshToken")] public required TokenInfo RefreshToken { get; init; }
}

public sealed record AuthenticationTokenRefreshResponse
{
    [JsonPropertyName("accessToken")] public required TokenInfo AccessToken { get; init; }
}

public sealed record OperationStatus
{
    [JsonPropertyName("code")] public int Code { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("details")] public List<string>? Details { get; init; }
}

public sealed record AuthenticationOperationStatusResponse
{
    [JsonPropertyName("status")] public required OperationStatus Status { get; init; }
}


public sealed record DateRange
{
    [JsonPropertyName("dateType")] public required string DateType { get; init; }
    [JsonPropertyName("from")] public DateTimeOffset From { get; init; }
    [JsonPropertyName("to")] public DateTimeOffset To { get; init; }
}

public sealed record InvoiceQueryFilters
{
    [JsonPropertyName("subjectType")] public required string SubjectType { get; init; }
    [JsonPropertyName("dateRange")] public required DateRange DateRange { get; init; }
}

public sealed record QueryInvoicesMetadataResponse
{
    [JsonPropertyName("hasMore")] public bool HasMore { get; init; }
    [JsonPropertyName("isTruncated")] public bool IsTruncated { get; init; }
    [JsonPropertyName("permanentStorageHwmDate")] public DateTimeOffset? PermanentStorageHwmDate { get; init; }
    [JsonPropertyName("invoices")] public List<KSeXInvoiceMetadata> Invoices { get; init; } = new();
}


public sealed record SellerInfo
{
    [JsonPropertyName("nip")] public string? Nip { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
}

public sealed record BuyerIdentifier
{
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("value")] public string? Value { get; init; }
}

public sealed record BuyerInfo
{
    [JsonPropertyName("identifier")] public BuyerIdentifier? Identifier { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
}

public sealed record KSeXInvoiceMetadata
{
    [JsonPropertyName("ksefNumber")] public required string KsefNumber { get; init; }
    [JsonPropertyName("invoiceNumber")] public string? InvoiceNumber { get; init; }
    [JsonPropertyName("issueDate")] public string? IssueDate { get; init; }
    [JsonPropertyName("invoicingDate")] public DateTimeOffset? InvoicingDate { get; init; }
    [JsonPropertyName("acquisitionDate")] public DateTimeOffset? AcquisitionDate { get; init; }
    [JsonPropertyName("permanentStorageDate")] public DateTimeOffset? PermanentStorageDate { get; init; }
    [JsonPropertyName("seller")] public SellerInfo? Seller { get; init; }
    [JsonPropertyName("buyer")] public BuyerInfo? Buyer { get; init; }
    [JsonPropertyName("netAmount")] public decimal? NetAmount { get; init; }
    [JsonPropertyName("vatAmount")] public decimal? VatAmount { get; init; }
    [JsonPropertyName("grossAmount")] public decimal? GrossAmount { get; init; }
    [JsonPropertyName("currency")] public string? Currency { get; init; }
    [JsonPropertyName("invoicingMode")] public string? InvoicingMode { get; init; }
    [JsonPropertyName("invoiceType")] public string? InvoiceType { get; init; }
    [JsonPropertyName("hasAttachment")] public bool? HasAttachment { get; init; }
    [JsonPropertyName("isSelfInvoicing")] public bool? IsSelfInvoicing { get; init; }
    [JsonPropertyName("invoiceHash")] public string? InvoiceHash { get; init; }
}

