using System.Globalization;

namespace LaLlamaDelBosque.Utils
{
    public static class MoneyExtensions
    {
        public static string ToCRC(this int value)
        {
            return "₡ " + value.ToString("N0", CultureInfo.InvariantCulture);
        }

        // Double
        public static string ToCRC(this double value)
        {
            return "₡ " + value.ToString("N0", CultureInfo.InvariantCulture);
        }

        // Decimal (recomendado para dinero)
        public static string ToCRC(this decimal value)
        {
            return "₡ " + value.ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
