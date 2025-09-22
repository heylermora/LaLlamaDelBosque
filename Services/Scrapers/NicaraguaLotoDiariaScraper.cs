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

			// NICA: mapa literal "12PM", "3PM", "6PM", "9PM"
			var hourToLottery = scrapingLotteries
				.Where(x => x.Type == "NICA" && !string.IsNullOrWhiteSpace(x.Hour))
				.ToDictionary(x => x.Hour!.Trim().ToUpperInvariant(), x => x);

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

			if(sectionNodes == null || sectionNodes.Count == 0) return awardLines;

			foreach(var section in sectionNodes)
			{
				// 1) Hora desde el H2 que contiene "SORTEO …"
				var hourH2 = section.SelectSingleNode(".//h2[contains(.,'SORTEO')]");
				if(hourH2 == null) continue;

				var m = SorteoHour.Match(Clean(hourH2.InnerText));
				if(!m.Success) continue;

				var hourKey = $"{int.Parse(m.Groups[1].Value)}:00 {m.Groups[2].Value.ToUpperInvariant()}";

				if(!hourToLottery.TryGetValue(hourKey, out var matchedLottery))
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

				var multiXH3 = section.SelectSingleNode(".//h3[contains(.,'(Multi X)')]");
				if(multiXH3 != null)
				{
					var mt = MultiXRegex.Match(Clean(multiXH3.InnerText));
					if(mt.Success)
					{
						multiX = mt.Groups[1].Value.ToUpperInvariant(); // "JG", "2X", "3X", "XX", etc.
						isBusted = Constants.BustedList.Contains(multiX);
					}
				}

				var line = CreateAwardLine(order, description, numberText, isBusted, papers);
				if(line != null) awardLines.Add(line);
			}

			return awardLines;
		}

		private static string Clean(string? s) =>
			Regex.Replace(HtmlEntity.DeEntitize(s ?? "").Replace('\u00A0', ' '), @"\s+", " ").Trim();
	}
}
