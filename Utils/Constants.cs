namespace LaLlamaDelBosque.Utils
{
	public static class Constants
	{
		public static readonly Dictionary<int, string> LimitStatus = new Dictionary<int, string>()
		{
			{ 1, "Sin límite" },
			{ 2, "Límite no excedido" },
			{ 3, "Límite alcanzado" },
			{ 4, "Límite excedido" }
		};
	}
}