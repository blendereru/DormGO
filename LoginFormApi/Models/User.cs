namespace LoginFormApi.Models;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public bool HasValidPassword()
    {
        // Minimum password length
        const int minLength = 8;

        // Check for minimum length
        if (Password.Length < minLength)
        {
            return false;
        }

        // Check for at least one uppercase letter
        if (!Password.Any(char.IsUpper))
        {
            return false;
        }

        // Check for at least one lowercase letter
        if (!Password.Any(char.IsLower))
        {
            return false;
        }

        // Check for at least one digit
        if (!Password.Any(char.IsDigit))
        {
            return false;
        }

        // Check for at least one special character
        if (!Password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            return false;
        }

        // All checks passed
        return true;
    }
}