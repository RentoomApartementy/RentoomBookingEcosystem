using System;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace RentoomBooking.SharedClasses.Utils
{
    public static class ApartmentObjectHasher
    {
       
        public static string ComputeHash(object obj)
        {
            
            var json = JsonConvert.SerializeObject(obj);

            
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                var hashBytes = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}
