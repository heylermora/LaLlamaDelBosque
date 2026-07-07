using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using System.Text.RegularExpressions;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public class HondurasLotoDiariaScraper: BaseScraper
	{
		private static readonly Regex SorteoHour = new(@"SORTEO\s+(\d{1,2}):00\s*([AP])\.?\s*M\.?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex DrawNumber = new(@"\b(\d)\s+(\d)\s+(\d)\b", RegexOptions.Compiled);

		public HondurasLotoDiariaScraper(HttpClient httpClient)
			: base(httpClient, "https://loto.hn/?pag=diaria")
		{
		}

		protected override List<AwardLine> ProcessHtml(string htmlContent, List<ScrapingLottery> scrapingLotteries, List<Lottery> lotteries, List<Paper> papers)
		{
			var awardLines = new List<AwardLine>();

			var hourToLottery = scrapingLotteries
				.Where(x => string.IsNullOrWhiteSpace(x.Type) && x.Name.Contains("Diaria", StringComparison.OrdinalIgnoreCase))
				.GroupBy(x => x.Hour.Trim().ToUpperInvariant())
				.ToDictionary(x => x.Key, x => x.First());

			if(hourToLottery.Count == 0)
				return awardLines;

			var orderToName = lotteries
				.GroupBy(x => x.Order)
				.ToDictionary(x => x.Key, x => x.First().Name);

			var doc = new HtmlDocument();
			doc.LoadHtml(htmlContent);

			var textLines = ExtractTextLines(doc);
			for(var index = 0; index < textLines.Count; index++)
			{
				var hourMatch = SorteoHour.Match(textLines[index]);
				if(!hourMatch.Success)
					continue;

				var hourKey = $"{int.Parse(hourMatch.Groups[1].Value)}:00 {hourMatch.Groups[2].Value.ToUpperInvariant()}M";
				if(!hourToLottery.TryGetValue(hourKey, out var matchedLottery))
					continue;

				var numberText = FindNumberText(textLines, index + 1);
				if(string.IsNullOrWhiteSpace(numberText))
					continue;

				var number = numberText[..2];
				var order = matchedLottery.Order;
				var description = orderToName.TryGetValue(order, out var name) ? name : string.Empty;
				var awardLine = CreateAwardLine(order, description, number, false, papers);

				if(awardLine != null)
					awardLines.Add(awardLine);
			}

			return awardLines;
		}

		private static List<string> ExtractTextLines(HtmlDocument doc)
		{
			return doc.DocumentNode
				.Descendants()
				.Where(x => !x.HasChildNodes)
				.Select(x => Clean(x.InnerText))
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.ToList();
		}

		private static string FindNumberText(List<string> textLines, int startIndex)
		{
			var digits = new List<string>();

			for(var index = startIndex; index < textLines.Count; index++)
			{
				if(SorteoHour.IsMatch(textLines[index]))
					return string.Empty;

				var numberMatch = DrawNumber.Match(textLines[index]);
				if(numberMatch.Success)
					return $"{numberMatch.Groups[1].Value}{numberMatch.Groups[2].Value}{numberMatch.Groups[3].Value}";

				if(Regex.IsMatch(textLines[index], @"^\d$"))
				{
					digits.Add(textLines[index]);
					if(digits.Count == 3)
						return string.Join(string.Empty, digits);
				}
				else if(digits.Count > 0)
				{
					digits.Clear();
				}
			}

			return string.Empty;
		}

		private static string Clean(string? value)
		{
			return Regex.Replace(HtmlEntity.DeEntitize(value ?? string.Empty).Replace('\u00A0', ' '), @"\s+", " ").Trim();
		}
	}
}
