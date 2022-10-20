using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;

namespace WebReaper.Domain.Parsing;

public record Html(string Field, string Selector)
    : SchemaElement(Field, Selector)
{
    public override string GetData(HtmlDocument doc)
    {
        var node = doc.DocumentNode.QuerySelector(Selector);

        var content = node?.InnerHtml;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException($"No html found by selector {Selector} in {node?.OuterHtml}.");
        }

        return HtmlEntity.DeEntitize(content);
    }
}