using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public class DominicanaLaPrimeraScraper: MultiSourceScraper
	{
		private const string LoteriasDominicanasUrl = "https://loteriasdominicanas.com/";
		private const string ConectateUrl = "https://www.conectate.com.do/loterias/la-primera/";
		private static readonly Regex TwoDigits = new(@"^\d{2}$", RegexOptions.Compiled);
		private static readonly IReadOnlyList<ScrapingSource> Sources = new[]
		{
			new ScrapingSource(LoteriasDominicanasUrl, "https://loteriasdominicanas.com/"),
			new ScrapingSource(ConectateUrl, "https://www.conectate.com.do/")
		};

		public DominicanaLaPrimeraScraper(HttpClient httpClient, TimeProvider timeProvider)
			: base(httpClient, Sources, timeProvider)
		{
		}

		protected override IEnumerable<int> GetExpectedOrders(List<ScrapingLottery> scrapingLotteries)
		{
			return scrapingLotteries.Where(IsLaPrimera).Select(x => x.Order).Distinct();
		}

		protected override string GetAllSourcesFailedMessage()
		{
			return "No fue posible obtener los resultados de La Primera desde ninguna de las fuentes configuradas.";
		}

		protected override List<AwardLine> ProcessHtml(
			string htmlContent,
			List<ScrapingLottery> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers,
			ScrapingSource source)
		{
			var drawToLottery = scrapingLotteries
				.Where(IsLaPrimera)
				.GroupBy(GetConfiguredDrawKey)
				.ToDictionary(x => x.Key, x => x.First());
			var orderToName = lotteries
				.GroupBy(x => x.Order)
				.ToDictionary(x => x.Key, x => x.First().Name);
			var document = new HtmlDocument();
			document.LoadHtml(htmlContent);
			var textLines = ExtractTextLines(document);
			var awardLines = new List<AwardLine>();

			for(var index = 0; index < textLines.Count; index++)
			{
				var drawKey = GetDrawKey(textLines[index]);
				if(string.IsNullOrWhiteSpace(drawKey) && Normalize(textLines[index]).Contains("primera", StringComparison.Ordinal))
					drawKey = GetDrawKey(string.Join(" ", textLines.Skip(index).Take(3)));
				if(string.IsNullOrWhiteSpace(drawKey) || !drawToLottery.TryGetValue(drawKey, out var configuredLottery))
					continue;

				var number = FindNextNumber(textLines, index + 1);
				if(string.IsNullOrWhiteSpace(number))
					continue;

				var description = orderToName.TryGetValue(configuredLottery.Order, out var lotteryName)
					? lotteryName
					: string.Empty;
				var awardLine = CreateAwardLine(configuredLottery.Order, description, number, false, papers);
				if(awardLine != null)
					awardLines.Add(awardLine);
			}

			return awardLines
				.GroupBy(x => x.Order)
				.Select(x => x.First())
				.ToList();
		}

		private static List<string> ExtractTextLines(HtmlDocument document)
		{
			return document.DocumentNode
				.Descendants()
				.Where(x => !x.HasChildNodes && !x.Ancestors("script").Any() && !x.Ancestors("style").Any())
				.Select(x => Clean(x.InnerText))
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.ToList();
		}

		private static string FindNextNumber(List<string> textLines, int startIndex)
		{
			for(var index = startIndex; index < textLines.Count && index < startIndex + 30; index++)
			{
				if(!string.IsNullOrWhiteSpace(GetDrawKey(textLines[index])))
					return string.Empty;

				if(TwoDigits.IsMatch(textLines[index]))
					return textLines[index];

				if(IsDate(textLines[index]))
					continue;
				if(Regex.IsMatch(textLines[index], @"^\d{1,2}:\d{2}\s*[ap]\.?\s*m\.?$", RegexOptions.IgnoreCase))
					continue;

				var twoDigitNumber = Regex.Match(textLines[index], @"\b\d{2}\b");
				if(twoDigitNumber.Success)
					return twoDigitNumber.Value;

				var separatedDigits = Regex.Match(textLines[index], @"(?:^|\D)(\d)\s+(\d)(?:\s+\d)?(?:\D|$)");
				if(separatedDigits.Success)
					return $"{separatedDigits.Groups[1].Value}{separatedDigits.Groups[2].Value}";
			}

			return string.Empty;
		}

		private static bool IsLaPrimera(ScrapingLottery lottery)
		{
			return lottery.Type.Equals("LA PRIMERA", StringComparison.OrdinalIgnoreCase);
		}

		private static string GetConfiguredDrawKey(ScrapingLottery lottery)
		{
			if(!string.IsNullOrWhiteSpace(lottery.SourceKey))
				return Normalize(lottery.SourceKey);

			return Normalize(lottery.Name).Contains("noche", StringComparison.Ordinal) ? "noche" : "dia";
		}

		private static string GetDrawKey(string value)
		{
			var normalized = Normalize(value);
			if(!normalized.Contains("primera", StringComparison.Ordinal))
				return string.Empty;

			if(normalized.Contains("noche", StringComparison.Ordinal))
				return "noche";
			if(normalized.Contains("dia", StringComparison.Ordinal)
				|| normalized.Contains("mediodia", StringComparison.Ordinal)
				|| normalized.Contains("matutina", StringComparison.Ordinal))
				return "dia";

			return string.Empty;
		}

		private static bool IsDate(string value)
		{
			return Regex.IsMatch(value, @"\b\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4}\b")
				|| Regex.IsMatch(value, @"\b\d{1,2}\s+de\s+[a-záéíóúñ]+\s+(?:de\s+)?\d{4}\b", RegexOptions.IgnoreCase);
		}

		private static string Normalize(string value)
		{
			var decomposed = Clean(value).ToLowerInvariant().Normalize(NormalizationForm.FormD);
			return new string(decomposed.Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark).ToArray())
				.Normalize(NormalizationForm.FormC);
		}

		private static string Clean(string? value)
		{
			return Regex.Replace(HtmlEntity.DeEntitize(value ?? string.Empty).Replace('\u00A0', ' '), @"\s+", " ").Trim();
		}
	}
}
