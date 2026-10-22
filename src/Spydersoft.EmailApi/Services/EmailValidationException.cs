namespace Spydersoft.EmailApi.Services;

public sealed class EmailValidationException(string message, bool tooLarge = false) : Exception(message)
{
    public bool TooLarge { get; } = tooLarge;
}
