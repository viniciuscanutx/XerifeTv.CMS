using System.ComponentModel.DataAnnotations;

namespace XerifeTv.CMS.Modules.User.Dtos.Request;

public sealed class AdminChangePasswordRequestDto
{
    [Required]
    public string Id { get; set; } = string.Empty;
    [Required(ErrorMessage = "Informe a nova senha.")]
    [StringLength(256, ErrorMessage = "A senha deve ter no máximo 256 caracteres.")]
    public string NewPassword { get; set; } = string.Empty;
    [Compare(nameof(NewPassword), ErrorMessage = "As senhas não coincidem.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
