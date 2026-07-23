using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Services.Blog;
using Xunit;

namespace SharedClasses.Tests;

public class BlogApartmentSelectionTests
{
    private static IReadOnlyList<ApartmentObject> Active(params int[] ids)
        => ids.Select(id => new ApartmentObject { Id = id, Name = $"Apartment {id}" }).ToList();

    [Fact]
    public void SelectOrdered_EmptyIds_ReturnsAllInDefaultOrder()
    {
        var active = Active(5, 3, 1);

        var result = BlogApartmentSelection.SelectOrdered(active, Array.Empty<int>());

        Assert.Equal(new[] { 5, 3, 1 }, result.Select(a => a.Id));
    }

    [Fact]
    public void SelectOrdered_ExplicitIds_ReturnsOnlyThoseInGivenOrder()
    {
        var active = Active(1, 2, 3);

        var result = BlogApartmentSelection.SelectOrdered(active, new[] { 3, 1, 2 });

        Assert.Equal(new[] { 3, 1, 2 }, result.Select(a => a.Id));
    }

    [Fact]
    public void SelectOrdered_SkipsIdsNotInActiveSet()
    {
        var active = Active(1, 2, 3);

        var result = BlogApartmentSelection.SelectOrdered(active, new[] { 2, 99, 1 });

        Assert.Equal(new[] { 2, 1 }, result.Select(a => a.Id));
    }

    [Fact]
    public void SelectOrdered_NoActiveApartments_ReturnsEmpty()
    {
        var result = BlogApartmentSelection.SelectOrdered(Array.Empty<ApartmentObject>(), new[] { 1, 2 });

        Assert.Empty(result);
    }
}
