using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.Storage
{
    public class StorageOptions
    {
        public string Container { get; set; } = "uploads";
        public string? ConnectionString { get; set; }
        public string? AccountName { get; set; }
        public bool HasAzureConfiguration()
        {
            return !string.IsNullOrWhiteSpace(ConnectionString) || !string.IsNullOrWhiteSpace(AccountName);
        }
    }
}
