using RentoomBookingWeb.Models;
using System.Threading.Tasks;

namespace RentoomBookingWeb.Services
{
    public interface IGusService
    {
        Task<GusCompanyData> GetCompanyInfoByNipAsync(string nip);
    }
}