using System.ComponentModel.DataAnnotations;

namespace SocksShoppingStore.Models
{
    public class CheckoutViewModel
    {
        [Required, Display(Name = "Full name")]
        public string CustomerName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, Display(Name = "Address line 1")]
        public string AddressLine1 { get; set; } = string.Empty;

        [Display(Name = "Address line 2")]
        public string? AddressLine2 { get; set; }

        [Required]
        public string City { get; set; } = string.Empty;

        [Required, Display(Name = "Postal code")]
        public string PostalCode { get; set; } = string.Empty;

        [Required]
        public string Country { get; set; } = string.Empty;

        // Honeypot (must remain empty)
        public string? Website { get; set; }

        // Shipping and promotions (dev/demo)
        public string? ShippingMethod { get; set; }
        public string? PromoCode { get; set; }
    }
}
