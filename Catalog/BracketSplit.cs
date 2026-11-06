namespace OneApi.Common.Catalog;

/// <summary>模型 id 拆掉方括号参数后的两段：<c>base[params]</c> → <c>base</c> 和 <c>params</c>（没有或为空时是 null）。</summary>
public readonly record struct BracketSplit(string BaseSlug, string? BracketParams);
