using Iridium.Enums.Resources;

namespace Iridium.Extensions.Resources;

public static class ResourceSortExtensions {

    public static string ToModrinthIndex(this ResourceSort sort) => sort switch {
        ResourceSort.Downloads or ResourceSort.TotalDownloads => "downloads",
        ResourceSort.Follows => "follows",
        ResourceSort.Newest or ResourceSort.ReleasedDate => "newest",
        ResourceSort.Updated or ResourceSort.LastUpdated => "updated",
        _ => "relevance"
    };


    public static int ToCurseForgeSortField(this ResourceSort sort) => sort switch {
        ResourceSort.Popularity => 2,
        ResourceSort.Updated or ResourceSort.LastUpdated => 3,
        ResourceSort.Name => 4,
        ResourceSort.Author => 5,
        ResourceSort.Downloads or ResourceSort.TotalDownloads => 6,
        ResourceSort.Newest or ResourceSort.ReleasedDate => 11,
        ResourceSort.Follows => 12,
        ResourceSort.Rating => 13,
        _ => 4
    };
}
