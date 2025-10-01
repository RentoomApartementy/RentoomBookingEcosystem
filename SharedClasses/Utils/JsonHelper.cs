using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RentoomBooking.SharedClasses.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Utils
{
    public static class JsonHelper //to ommit null parameters in json.
    {

        public static string TruncateFromLeftToColon(string input)
        {
            // Find the index of the colon
            int colonIndex = input.IndexOf(':');

            // Check if colon is found
            if (colonIndex == -1)
            {
                // Colon not found, return the original string or handle as needed
                return input;
            }

            // Extract the substring after the colon
            return input.Substring(colonIndex + 1).Trim();
        }


        public static string SerializeOnlyNonNullProperties<T>(T obj)
        {
            var settings = new JsonSerializerSettings
            {
            
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
                
            };
            return JsonConvert.SerializeObject(obj, Formatting.None, settings);
        }

        public static bool Md5CompareStrings(string string1, string string2)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash1 = md5.ComputeHash(Encoding.UTF8.GetBytes(string1));
                byte[] hash2 = md5.ComputeHash(Encoding.UTF8.GetBytes(string2));

                string hashString1 = BitConverter.ToString(hash1).Replace("-", "").ToLower();
                string hashString2 = BitConverter.ToString(hash2).Replace("-", "").ToLower();

                return hashString1 == hashString2;
            }
        }

        public static string MD5CreateSum(string stringtoHash)

        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash1 = md5.ComputeHash(Encoding.UTF8.GetBytes(stringtoHash));


                string hashString1 = BitConverter.ToString(hash1).Replace("-", "").ToLower();


                return hashString1;
            }
        }

    }
}
