using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models
{
    public sealed record PagedResult<T>(
        IReadOnlyList<T> Items, 
        string? ContinuationToken, 
        int CountOnPage,
        long TotalCount);
}
