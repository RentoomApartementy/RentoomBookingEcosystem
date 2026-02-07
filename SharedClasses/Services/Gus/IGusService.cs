using RentoomBooking.SharedClasses.Models.Gus;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.Gus
{
    public interface IGusService
    {
        Task<GusCompanyData> GetCompanyInfoByNipAsync(string nip);
    }
}