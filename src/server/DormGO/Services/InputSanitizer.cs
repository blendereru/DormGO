namespace DormGO.Services;

public class InputSanitizer : IInputSanitizer
{
    private readonly ILogger<InputSanitizer> _logger;
    public InputSanitizer(ILogger<InputSanitizer> logger)
    {
        _logger = logger;
    }
    public string Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            _logger.LogWarning("Sanitize called with null or empty input.");
            return string.Empty;
        }
        var sanitized = input
            .Replace("\n", "")
            .Replace("\r", "")
            .Replace("\t", "");
        if (sanitized != input)
        {
            _logger.LogInformation("Input sanitized. Original: '{OriginalInput}', Sanitized: '{SanitizedInput}'", input, sanitized);
        }
        else
        {
            _logger.LogDebug("Input did not require sanitization: '{Input}'", input);
        }
        return sanitized;
    }
}