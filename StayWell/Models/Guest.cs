namespace RentoomBooking.StayWell.Models
{
    public class Guest
    {
        private string _firstName = string.Empty;
        private string _lastName = string.Empty;
        private string _email = string.Empty;
        private string _phone = string.Empty;
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
        public string Email
        {
            get => _email;
            set => _email = value ?? string.Empty;
        }
        public string Phone
        {
            get => _phone;
            set => _phone = value ?? string.Empty;
        }
    }
}
