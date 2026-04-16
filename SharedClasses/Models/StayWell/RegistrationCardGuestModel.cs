using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.StayWell
{
    public class RegistrationCardGuestModel
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        
        private static readonly Regex NameRegex = new(
            @"^[\p{L}]+(['\-.\s][\p{L}]+)*$",
            RegexOptions.Compiled);

        public static bool IsValidName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            return NameRegex.IsMatch(name.Trim());
        }
    }
}
