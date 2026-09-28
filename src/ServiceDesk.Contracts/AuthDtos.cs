using System.ComponentModel.DataAnnotations;

namespace ServiceDesk.Contracts;

public class LoginRequest
{
    [Required(ErrorMessage = "Укажите телефон или email")]
    public string Login { get; set; }

    [Required(ErrorMessage = "Укажите пароль")]
    public string Password { get; set; }
}

public class RegisterRequest
{
    [Required(ErrorMessage = "Укажите фамилию"), StringLength(60)]
    public string LastName { get; set; }

    [Required(ErrorMessage = "Укажите имя"), StringLength(60)]
    public string FirstName { get; set; }

    [Required(ErrorMessage = "Укажите телефон")]
    public string Phone { get; set; }

    [EmailAddress(ErrorMessage = "Некорректный email"), StringLength(120)]
    public string Email { get; set; }

    [Required(ErrorMessage = "Укажите пароль")]
    public string Password { get; set; }
}

public class AuthResponse
{
    public string Token { get; set; }
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string Role { get; set; }
    public string RoleName { get; set; }
    public string LastName { get; set; }
    public string FirstName { get; set; }
    public string MiddleName { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public string FullName { get; set; }
    public string ShortName { get; set; }
}

public class ProfileUpdateRequest
{
    [Required(ErrorMessage = "Укажите фамилию"), StringLength(60)]
    public string LastName { get; set; }

    [Required(ErrorMessage = "Укажите имя"), StringLength(60)]
    public string FirstName { get; set; }

    [EmailAddress(ErrorMessage = "Некорректный email"), StringLength(120)]
    public string Email { get; set; }

    [Required(ErrorMessage = "Укажите телефон")]
    public string Phone { get; set; }
}

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Укажите текущий пароль")]
    public string CurrentPassword { get; set; }

    [Required(ErrorMessage = "Укажите новый пароль")]
    public string NewPassword { get; set; }
}

public class ApiError
{
    public string Message { get; set; }
}
