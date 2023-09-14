namespace LaLlamaDelBosque.Utils
{
	using System;

	public class TimeZoneHelper
	{
		private static TimeZoneInfo CostaRicaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

		public TimeZoneHelper()
		{
			CurrentDateTimeInCostaRica = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CostaRicaTimeZone);
		}

		public DateTime CurrentDateTimeInCostaRica { get; set; }
	}

}
