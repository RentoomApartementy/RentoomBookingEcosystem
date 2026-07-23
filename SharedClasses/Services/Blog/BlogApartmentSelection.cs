using RentoomBooking.SharedClasses.Models.IdoBooking;

namespace RentoomBooking.SharedClasses.Services.Blog;

/// <summary>
/// Selection rules for the blog ApartmentsListing block, kept pure so they can be unit tested
/// independently of the Blazor renderer and the apartments data source.
/// </summary>
public static class BlogApartmentSelection
{
    /// <summary>
    /// Picks the apartments to display for an ApartmentsListing block.
    /// An empty id list means "all active apartments" in their default order.
    /// A non-empty id list yields exactly those apartments, in the given order, skipping ids not present in the active set.
    /// </summary>
    public static IReadOnlyList<ApartmentObject> SelectOrdered(
        IReadOnlyList<ApartmentObject> activeApartments,
        IReadOnlyList<int> apartmentIds)
    {
        if (activeApartments is null || activeApartments.Count == 0)
        {
            return Array.Empty<ApartmentObject>();
        }

        if (apartmentIds is null || apartmentIds.Count == 0)
        {
            return activeApartments;
        }

        var byId = new Dictionary<int, ApartmentObject>();
        foreach (var apartment in activeApartments)
        {
            byId.TryAdd(apartment.Id, apartment);
        }

        var result = new List<ApartmentObject>(apartmentIds.Count);
        foreach (var id in apartmentIds)
        {
            if (byId.TryGetValue(id, out var apartment))
            {
                result.Add(apartment);
            }
        }

        return result;
    }
}
