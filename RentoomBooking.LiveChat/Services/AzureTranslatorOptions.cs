using System.ComponentModel.DataAnnotations;

namespace RentoomBooking.LiveChat.Services;

public sealed class AzureTranslatorOptions
{
    [Required]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public string Region { get; set; } = string.Empty;

    [Required]
    public string DefaultSourceLanguage { get; set; } = "auto";

    [Required]
    public string DefaultTargetLanguage { get; set; } = "pl";
}
