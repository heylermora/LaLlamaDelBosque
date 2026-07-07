using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using System.Text.RegularExpressions;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public class JpsNuevosTiemposScraper: BaseScraper
	{
		private static readonly Regex HourLine = new(@"^(\d{1,2})(?::(\d{2}))?\s*([AP])\.?\s*M\.?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex TwoDigits = new(@"^\d{2}$", RegexOptions.Compiled);

		public JpsNuevosTiemposScraper(HttpClient httpClient)
			: base(httpClient, "https://nicatiempos.com/")
		{
		}

		protected override List<AwardLine> ProcessHtml(string htmlContent, List<ScrapingLottery> scrapingLotteries, List<Lottery> lotteries, List<Paper> papers)
		{
			var awardLines = new List<AwardLine>();
			var sourceKeyToLottery = scrapingLotteries
				.Where(HasJpsSourceKey)
				.GroupBy(x => NormalizeSourceKey(x.SourceKey))
				.ToDictionary(x => x.Key, x => x.First());

			if(sourceKeyToLottery.Count == 0)
				return awardLines;

			var orderToName = lotteries
				.GroupBy(x => x.Order)
				.ToDictionary(x => x.Key, x => x.First().Name);

			var doc = new HtmlDocument();
			doc.LoadHtml(htmlContent);
			var textLines = ExtractTextLines(doc);

			for(var index = 0; index < textLines.Count; index++)
			{
				var hourMatch = HourLine.Match(textLines[index]);
				if(!hourMatch.Success)
					continue;

				var sourceKey = ToSourceKey(hourMatch);
				if(string.IsNullOrWhiteSpace(sourceKey) || !sourceKeyToLottery.TryGetValue(sourceKey, out var scrapingLottery))
					continue;

				if(!IsCostaRicaDraw(textLines, index + 1))
					continue;

				var number = FindNextNumber(textLines, index + 1);
				if(string.IsNullOrWhiteSpace(number))
					continue;

				var description = orderToName.TryGetValue(scrapingLottery.Order, out var name) ? name : string.Empty;
				var awardLine = CreateAwardLine(scrapingLottery.Order, description, number, false, papers);
				if(awardLine != null)
					awardLines.Add(awardLine);
			}

			return awardLines
				.GroupBy(x => x.Order)
				.Select(x => x.First())
				.ToList();
		}

		private static bool HasJpsSourceKey(ScrapingLottery scrapingLottery)
		{
			var sourceKey = NormalizeSourceKey(scrapingLottery.SourceKey);
			return sourceKey is "manana" or "tarde" or "noche";
		}

		private static string NormalizeSourceKey(string sourceKey)
		{
			return sourceKey
				.Trim()
				.ToLowerInvariant()
				.Replace("ñ", "n");
		}

		private static string ToSourceKey(Match hourMatch)
		{
			var hour = int.Parse(hourMatch.Groups[1].Value);
			var minutes = string.IsNullOrWhiteSpace(hourMatch.Groups[2].Value) ? 0 : int.Parse(hourMatch.Groups[2].Value);
			var period = hourMatch.Groups[3].Value.ToUpperInvariant();

			return (hour, minutes, period) switch
			{
				(1, 0, "P") => "manana",
				(4, 30, "P") => "tarde",
				(7, 30, "P") => "noche",
				_ => string.Empty
			};
		}

		private static bool IsCostaRicaDraw(List<string> textLines, int startIndex)
		{
			for(var index = startIndex; index < textLines.Count; index++)
			{
				if(HourLine.IsMatch(textLines[index]))
					return false;

				if(textLines[index].Contains("Nuevos Tiempos", StringComparison.OrdinalIgnoreCase)
					|| textLines[index].Contains("Costa Rica", StringComparison.OrdinalIgnoreCase)
					|| textLines[index].Contains("CR", StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
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

		private static string FindNextNumber(List<string> textLines, int startIndex)
		{
			for(var index = startIndex; index < textLines.Count; index++)
			{
				if(HourLine.IsMatch(textLines[index]))
					return string.Empty;

				var candidates = Regex.Matches(textLines[index], @"\b\d{2}\b").Select(x => x.Value).ToList();
				if(candidates.Count > 0)
					return candidates[0];

				if(TwoDigits.IsMatch(textLines[index]))
					return textLines[index];
			}

			return string.Empty;
		}

		private static string Clean(string? value)
		{
			return Regex.Replace(HtmlEntity.DeEntitize(value ?? string.Empty).Replace('\u00A0', ' '), @"\s+", " ").Trim();
		}
	}
}
