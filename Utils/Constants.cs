namespace LaLlamaDelBosque.Utils
{
	public static class Constants
	{
		public static readonly Dictionary<int, string> LimitStatus = new()
		{
			{ 1, "Sin límite" },
			{ 2, "Límite no excedido" },
			{ 3, "Límite alcanzado" },
			{ 4, "Límite excedido" }
		};

		public static readonly List<string> BustedList = new() { "R", "3X", "5X" };
	}
}