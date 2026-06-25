using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using System.Text.RegularExpressions;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public class NicaraguaLotoDiariaScraper: BaseScraper
	{
		private static readonly Regex TwoDigits = new(@"^\d{2}$", RegexOptions.Compiled);
		private static readonly Regex SorteoHour = new(@"SORTEO\s+(\d{1,2})\s*([AP]M)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex MultiXRegex =	new(@"\(Multi\s*X\)\s*=\s*([A-Za-z0-9]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex PostedResult = new(@"SORTEO\s+(\d{1,2})\s*([AP]M)\s+(\d{2}|XX|[-—]+).*?\(M[aá]s\s*1\)\s*=\s*([A-Za-z0-9—-]+).*?(?:[│|]\s*)?\(Multi\s*X\)\s*=\s*([A-Za-z0-9—-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		public NicaraguaLotoDiariaScraper(HttpClient httpClient)
			: base(httpClient, "https://nicatiempos.com/")
		{
		}

		protected override List<AwardLine> ProcessHtml(
			string htmlContent,
			List<ScrapingLottery> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers)
		{
			var awardLines = new List<AwardLine>();

			// NICA: el JSON usa horas con formato "11:00 AM", "3:00 PM", "6:00 PM", "9:00 PM".
			var hourToLottery = scrapingLotteries
				.Where(x => x.Type == "NICA" && !string.IsNullOrWhiteSpace(x.Hour))
				.ToDictionary(x => NormalizeHourKey(x.Hour!), x => x);

			if(hourToLottery.Count == 0) return awardLines;

			var orderToName = lotteries
				.GroupBy(l => l.Order)
				.ToDictionary(g => g.Key, g => g.First().Name);

			var doc = new HtmlDocument();
			doc.LoadHtml(htmlContent);

			// Cada bloque "section" de sorteo dentro del loop-item
			var sectionNodes = doc.DocumentNode.SelectNodes(
				"//div[contains(@class,'e-loop-item')]//div[contains(@class,'e-con-full') and contains(@class,'e-con') and contains(@class,'e-child')][.//h2[contains(.,'SORTEO')]]"
			);

			if(sectionNodes == null || sectionNodes.Count == 0)
				return ProcessPlainText(doc, hourToLottery, orderToName, papers);

			foreach(var section in sectionNodes)
			{
				// 1) Hora desde el H2 que contiene "SORTEO …"
				var hourH2 = section.SelectSingleNode(".//h2[contains(.,'SORTEO')]");
				if(hourH2 == null) continue;

				var m = SorteoHour.Match(Clean(hourH2.InnerText));
				if(!m.Success) continue;

				var hourKey = NormalizeHourKey(m.Groups[1].Value, m.Groups[2].Value);

				if(!hourToLottery.TryGetValue(hourKey, out var matchedLottery))
					continue;

				if(matchedLottery.Order == 9 && DateTime.Today.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
					continue;

				// 2) Número dentro del mismo bloque: .ball h2
				var numberH2 =
					section.SelectSingleNode(".//div[contains(@class,'ball')]//h2") ??
					section.SelectSingleNode(".//div[contains(@class,'ball')]/h2");

				if(numberH2 == null) continue;                  // p.ej. 6PM no trae número aún
				var numberText = Clean(numberH2.InnerText);
				if(!TwoDigits.IsMatch(numberText)) continue;     // ignora "XX" u otros no-2-dígitos

				// 3) Crear AwardLine
				var order = matchedLottery.Order;
				var description = orderToName.TryGetValue(order, out var name) ? name : string.Empty;

				string? multiX = null;
				var isBusted = false;
				double? timesBusted = null;

				var multiXH3 = section.SelectSingleNode(".//h3[contains(.,'(Multi X)')]");
				if(multiXH3 != null)
				{
					var mt = MultiXRegex.Match(Clean(multiXH3.InnerText));
					if(mt.Success)
					{
						multiX = mt.Groups[1].Value.ToUpperInvariant(); // "JG", "2X", "3X", "XX", etc.
						isBusted = Constants.BustedList.Contains(multiX) || Constants.NicaBustedMultipliers.ContainsKey(multiX);
						if(Constants.NicaBustedMultipliers.TryGetValue(multiX, out var mappedTimesBusted))
							timesBusted = mappedTimesBusted;
					}
				}

				var line = CreateAwardLine(order, description, numberText, isBusted, papers, timesBusted);
				if(line != null) awardLines.Add(line);
			}

			return awardLines.Count > 0 ? awardLines : ProcessPlainText(doc, hourToLottery, orderToName, papers);
		}

		private List<AwardLine> ProcessPlainText(
			HtmlDocument doc,
			Dictionary<string, ScrapingLottery> hourToLottery,
			Dictionary<int, string> orderToName,
			List<Paper> papers)
		{
			var awardLines = new List<AwardLine>();
			var pageText = Clean(doc.DocumentNode.InnerText);

			foreach(Match match in PostedResult.Matches(pageText))
			{
				var hourKey = NormalizeHourKey(match.Groups[1].Value, match.Groups[2].Value);
				if(!hourToLottery.TryGetValue(hourKey, out var matchedLottery))
					continue;

				if(matchedLottery.Order == 9 && DateTime.Today.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
					continue;

				var numberText = match.Groups[3].Value;
				if(!TwoDigits.IsMatch(numberText))
					continue;

				var order = matchedLottery.Order;
				var description = orderToName.TryGetValue(order, out var name) ? name : string.Empty;
				var mas1 = match.Groups[4].Value.ToUpperInvariant();
				var isBusted = Constants.BustedList.Contains(mas1) || Constants.NicaBustedMultipliers.ContainsKey(mas1);
				double? timesBusted = null;

				if(Constants.NicaBustedMultipliers.TryGetValue(mas1, out var mappedTimesBusted))
					timesBusted = mappedTimesBusted;

				var line = CreateAwardLine(order, description, numberText, isBusted, papers, timesBusted);
				if(line != null) awardLines.Add(line);
			}

			return awardLines;
		}

		private static string NormalizeHourKey(string hour) =>
			Clean(hour).ToUpperInvariant();

		private static string NormalizeHourKey(string hour, string meridiem) =>
			$"{int.Parse(hour)}:00 {meridiem.ToUpperInvariant()}";

		private static string Clean(string? s) =>
			Regex.Replace(HtmlEntity.DeEntitize(s ?? "").Replace('\u00A0', ' '), @"\s+", " ").Trim();
	}
}
