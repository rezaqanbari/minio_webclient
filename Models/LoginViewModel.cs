using System.ComponentModel.DataAnnotations;

namespace minio_csharpClient.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "لطفاً نام کاربری را وارد نمایید.")]
    [Display(Name = "نام کاربری")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "لطفاً رمز عبور را وارد نمایید.")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "مرا به خاطر بسپار")]
    public bool RememberMe { get; set; } = true;
}
