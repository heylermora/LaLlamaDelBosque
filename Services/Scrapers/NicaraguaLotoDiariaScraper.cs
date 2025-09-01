using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public class NicaraguaLotoDiariaScraper: BaseScraper
	{
		private static readonly Regex TwoDigits = new(@"^\d{2}$", RegexOptions.Compiled);

		public NicaraguaLotoDiariaScraper(HttpClient httpClient)
			: base(httpClient, "https://tiemposnicas.com/")
		{
		}

		protected override List<AwardLine> ProcessHtml(
			string htmlContent,
			List<ScrapingLottery> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers)
		{
			var awardLines = new List<AwardLine>();

			var nicaLotteries = scrapingLotteries.Where(x => x.Type == "NICA").ToList();
			var hourToLottery = nicaLotteries
				.Select(l => new { Key = l.Hour, Value = l })
				.Where(x => x.Key != null)
				.ToDictionary(x => x.Key!, x => x.Value);

			if(hourToLottery.Count == 0) return awardLines;

			var orderToName = lotteries
				.GroupBy(l => l.Order)
				.ToDictionary(g => g.Key, g => g.First().Name);

			var doc = new HtmlDocument();
			doc.LoadHtml(htmlContent);

			var horaNodes = doc.DocumentNode.SelectNodes("//div[@class='hora']");
			if(horaNodes == null || horaNodes.Count == 0) return awardLines;

			foreach(var horaNode in horaNodes)
			{
				var horaText = Clean(horaNode.InnerText);
				var hourKey = horaText;
				if(hourKey == null) continue;

				if(!hourToLottery.TryGetValue(hourKey, out var matchedLottery))
					continue;

				var premioNode = horaNode.SelectSingleNode("following-sibling::div[@class='premio'][1]");
				if(premioNode == null) continue;

				var digitNodes = premioNode.SelectNodes(".//div[@class='numero']//span[@class='digito']");
				if(digitNodes == null || digitNodes.Count < 2) continue;

				var numberText = string.Concat(digitNodes.Select(d => Clean(d.InnerText)));
				if(!TwoDigits.IsMatch(numberText)) continue;

				// En el HTML actual no aparece plus (JG/2X/3X)
				var isBusted = false;

				var order = matchedLottery.Order;
				var description = orderToName.TryGetValue(order, out var name) ? name : string.Empty;

				var awardLine = CreateAwardLine(order, description, numberText, isBusted, papers);
				if(awardLine != null)
					awardLines.Add(awardLine);
			}

			return awardLines;
		}

		private static string Clean(string? s) =>
			Regex.Replace(HtmlEntity.DeEntitize(s ?? "").Replace('\u00A0', ' '), @"\s+", " ").Trim();
	}
}