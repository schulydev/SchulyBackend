namespace Schuly.Application.Commands.Me
{
    internal static class MyGrade
    {
        public const decimal MaxValue = 99.99m;

        public static string? Invalid(decimal score, decimal weight) =>
            score < 0 || score > MaxValue ? $"Score must be between 0 and {MaxValue}"
            : weight < 0 || weight > MaxValue ? $"Weight must be between 0 and {MaxValue}"
            : null;
    }
}
