namespace OneApi.Common.Catalog;

public record ModelSlugParts(
    string Raw,
    string SlugWithoutBrackets,
    string ProductBase,
    string BillingTier,
    bool Fast,
    string? BracketParams);
