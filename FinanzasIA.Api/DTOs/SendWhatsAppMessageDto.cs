using System.ComponentModel.DataAnnotations;

namespace FinanzasIA.Api.DTOs;

public class SendWhatsAppMessageDto
{
    [Required]
    public string To { get; set; } = string.Empty;

    [Required]
    [MaxLength(1024)]
    public string Message { get; set; } = string.Empty;
}
