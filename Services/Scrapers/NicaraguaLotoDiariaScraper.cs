using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using System.Text.RegularExpressions;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public class NicaraguaLotoDiariaScraper: BaseScraper
	{
		private static readonly Regex HourLine = new(@"^(\d{1,2}):00\s*([AP])\.?\s*M\.?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex DrawNumber = new(@"\b(\d)\s+(\d)\b", RegexOptions.Compiled);
		private static readonly Regex MultiXRegex = new(@"\b(JG|2X|3X|5X|7X|R)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		public NicaraguaLotoDiariaScraper(HttpClient httpClient)
			: base(httpClient, "https://tiemposhoy.com/nica-hoy")
		{
		}

		protected override List<AwardLine> ProcessHtml(
			string htmlContent,
			List<ScrapingLottery> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers)
		{
			var awardLines = new List<AwardLine>();

			var hourToLottery = scrapingLotteries
				.Where(x => x.Type == "NICA" && !string.IsNullOrWhiteSpace(x.Hour))
				.GroupBy(x => x.Hour.Trim().ToUpperInvariant())
				.ToDictionary(x => x.Key, x => x.First());

			if(hourToLottery.Count == 0)
				return awardLines;

			var orderToName = lotteries
				.GroupBy(l => l.Order)
				.ToDictionary(g => g.Key, g => g.First().Name);

			var doc = new HtmlDocument();
			doc.LoadHtml(htmlContent);

			var textLines = ExtractTextLines(doc);
			for(var index = 0; index < textLines.Count; index++)
			{
				var hourMatch = HourLine.Match(textLines[index]);
				if(!hourMatch.Success)
					continue;

				var hourKey = $"{int.Parse(hourMatch.Groups[1].Value)}:00 {hourMatch.Groups[2].Value.ToUpperInvariant()}M";
				if(!hourToLottery.TryGetValue(hourKey, out var matchedLottery))
					continue;

				if(matchedLottery.Order == 9 && DateTime.Today.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
					continue;

				var drawResult = FindDrawResult(textLines, index + 1);
				if(string.IsNullOrWhiteSpace(drawResult.Number))
					continue;

				var order = matchedLottery.Order;
				var description = orderToName.TryGetValue(order, out var name) ? name : string.Empty;
				var isBusted = Constants.BustedList.Contains(drawResult.MultiX) || Constants.NicaBustedMultipliers.ContainsKey(drawResult.MultiX);
				double? timesBusted = Constants.NicaBustedMultipliers.TryGetValue(drawResult.MultiX, out var mappedTimesBusted)
					? mappedTimesBusted
					: null;

				var line = CreateAwardLine(order, description, drawResult.Number, isBusted, papers, timesBusted);
				if(line != null)
					awardLines.Add(line);
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

		private static (string Number, string MultiX) FindDrawResult(List<string> textLines, int startIndex)
		{
			var digits = new List<string>();
			var number = string.Empty;
			var multiX = string.Empty;

			for(var index = startIndex; index < textLines.Count; index++)
			{
				if(HourLine.IsMatch(textLines[index]))
					return (number, multiX);

				var multiXMatch = MultiXRegex.Match(textLines[index]);
				if(multiXMatch.Success)
					multiX = multiXMatch.Groups[1].Value.ToUpperInvariant();

				if(string.IsNullOrWhiteSpace(number))
				{
					var numberMatch = DrawNumber.Match(textLines[index]);
					if(numberMatch.Success)
					{
						number = $"{numberMatch.Groups[1].Value}{numberMatch.Groups[2].Value}";
						continue;
					}

					if(Regex.IsMatch(textLines[index], @"^\d$"))
					{
						digits.Add(textLines[index]);
						if(digits.Count == 2)
							number = string.Join(string.Empty, digits);
					}
					else if(digits.Count > 0)
					{
						digits.Clear();
					}
				}
			}

			return (number, multiX);
		}

		private static string Clean(string? value)
		{
			return Regex.Replace(HtmlEntity.DeEntitize(value ?? string.Empty).Replace('\u00A0', ' '), @"\s+", " ").Trim();
		}
	}
}
