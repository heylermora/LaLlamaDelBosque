using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using System.Text.RegularExpressions;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public class JpsNuevosTiemposScraper: BaseScraper
	{
		private static readonly Regex DrawLabel = new(@"\b(Mediod[ií]a|Tarde|Noche)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex TwoDigits = new(@"^\d{2}$", RegexOptions.Compiled);

		public JpsNuevosTiemposScraper(HttpClient httpClient)
			: base(httpClient, "https://www.jps.go.cr/resultados")
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
				var drawLabelMatch = DrawLabel.Match(textLines[index]);
				if(!drawLabelMatch.Success)
					continue;

				var sourceKey = ToSourceKey(drawLabelMatch.Groups[1].Value);
				if(!sourceKeyToLottery.TryGetValue(sourceKey, out var scrapingLottery))
					continue;

				var number = FindNextNumber(textLines, index);
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

		private static string ToSourceKey(string drawLabel)
		{
			return NormalizeSourceKey(drawLabel).StartsWith("mediodia") ? "manana" : NormalizeSourceKey(drawLabel);
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
				if(index > startIndex && DrawLabel.IsMatch(textLines[index]))
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
