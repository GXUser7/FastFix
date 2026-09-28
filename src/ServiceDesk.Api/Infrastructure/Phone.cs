namespace ServiceDesk.Api.Infrastructure;

// Нормализация российских номеров к виду +7XXXXXXXXXX
public static class Phone
{
    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && (digits[0] == '7' || digits[0] == '8')) digits = digits[1..];
        return digits.Length == 10 ? "+7" + digits : null;
    }

    public static string Format(string phone)
    {
        if (phone == null || phone.Length != 12) return phone;
        return $"+7 {phone.Substring(2, 3)} {phone.Substring(5, 3)}-{phone.Substring(8, 2)}-{phone.Substring(10, 2)}";
    }

    // Цифры для частичного поиска по телефону («1122» → найдёт +79005551122)
    public static string SearchDigits(string input)
    {
        var digits = new string((input ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && (digits[0] == '8' || digits[0] == '7')) digits = digits[1..];
        return digits;
    }
}

public static class PasswordPolicy
{
    public const string Requirements = "Пароль должен содержать не менее 8 символов, буквы и цифры";

    public static bool IsValid(string password) =>
        !string.IsNullOrEmpty(password)
        && password.Length >= 8
        && password.Any(char.IsLetter)
        && password.Any(char.IsDigit);
}
