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
            .Replace("\\", "\\\\")
            .Replace("\n", "")
            .Replace("\r", "")
            .Replace("\t", "")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Trim();
        if (sanitized != input)
        {
            _logger.LogInformation("Input sanitized. Sanitized: {SanitizedInput}", sanitized);
        }
        else
        {
            _logger.LogDebug("Input did not require sanitization: {SanitizedInput}", sanitized);
        }
        return sanitized;
    }
}