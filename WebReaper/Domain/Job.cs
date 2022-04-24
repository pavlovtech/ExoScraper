using System.Collections.Immutable;
namespace WebReaper.Domain;

public record Job(
    SchemaElement[] schema,
    string BaseUrl,
    string Url,
    ImmutableQueue<LinkPathSelector> LinkPathSelectors,
    int DepthLevel = 0) {
        public PageType PageType { get {
            if(!LinkPathSelectors.Any()) {
                return PageType.TargetPage;
            }

            var currentSelector = LinkPathSelectors.Peek();

            if(LinkPathSelectors.Count() == 1 &&
                currentSelector.PaginationSelector != null) {
                return PageType.PageWithPagination;
            }

            return PageType.TransitPage;
        }}

        public int Priority => PageType switch
        {
            PageType.TargetPage => -int.MaxValue,
            PageType.PageWithPagination => -DepthLevel * (int)PageType.PageWithPagination,
            PageType.TransitPage => -DepthLevel * (int)PageType.TransitPage,
            PageType.Unknown => 1,
        };
    };
