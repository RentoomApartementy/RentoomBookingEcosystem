namespace RentoomBooking.StayWell.Models
{
    public class GuestData
    {
        private string _firstName = string.Empty;
        private string _lastName = string.Empty;
        public string FirstName
        {
            get => _firstName;
            set => _firstName = value ?? string.Empty;
        }
        public string LastName
        {
            get => _lastName;
            set => _lastName = value ?? string.Empty;
        }
    }
}
