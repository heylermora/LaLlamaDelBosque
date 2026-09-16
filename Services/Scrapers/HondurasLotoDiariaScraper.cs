using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public class HondurasLotoDiariaScraper: MultiSourceScraper
	{
		private const string YeluResultsUrl = "https://www.yelu.hn/lottery/results/la-diaria";
		private const string OfficialResultsUrl = "https://loto.hn/?pag=diaria";
		private static readonly CultureInfo HondurasCulture = CultureInfo.GetCultureInfo("es-HN");
		private static readonly Regex TwoDigits = new(@"^\d{2}$", RegexOptions.Compiled);
		private static readonly Regex OfficialDrawHeading = new(@"SORTEO\s+(\d{1,2}):00\s*([AP])\.?\s*M\.?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex OfficialDrawNumber = new(@"\b(\d)\s+(\d)\s+(\d)\b", RegexOptions.Compiled);
		private static readonly IReadOnlyList<ScrapingSource> Sources = new[]
		{
			new ScrapingSource(YeluResultsUrl, "https://www.yelu.hn/"),
			new ScrapingSource(OfficialResultsUrl, "https://loto.hn/")
		};

		public HondurasLotoDiariaScraper(HttpClient httpClient, TimeProvider timeProvider)
			: base(httpClient, Sources, timeProvider)
		{
		}

		protected override IEnumerable<int> GetExpectedOrders(List<ScrapingLottery> scrapingLotteries)
		{
			return scrapingLotteries.Where(IsHonduranLottery).Select(x => x.Order).Distinct();
		}

		protected override string GetAllSourcesFailedMessage()
		{
			return "No fue posible obtener los resultados de La Diaria Honduras desde ninguna de las fuentes configuradas.";
		}

		protected override List<AwardLine> ProcessHtml(
			string htmlContent,
			List<ScrapingLottery> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers,
			ScrapingSource source)
		{
			return source.Url == YeluResultsUrl
				? ProcessYeluHtml(htmlContent, scrapingLotteries, lotteries, papers)
				: ProcessOfficialHtml(htmlContent, scrapingLotteries, lotteries, papers);
		}

		private List<AwardLine> ProcessYeluHtml(
			string htmlContent,
			List<ScrapingLottery> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers)
		{
			var document = new HtmlDocument();
			document.LoadHtml(htmlContent);

			if(!ContainsTodaysResults(document))
				return new List<AwardLine>();

			var configuredLotteries = scrapingLotteries
				.Where(IsHonduranLottery)
				.GroupBy(x => Normalize(x.Name))
				.ToDictionary(x => x.Key, x => x.First());
			var orderToName = lotteries
				.GroupBy(x => x.Order)
				.ToDictionary(x => x.Key, x => x.First().Name);
			var resultNodes = document.DocumentNode.SelectNodes(
				"//div[contains(concat(' ', normalize-space(@class), ' '), ' lotto_numbers ')][.//div[contains(concat(' ', normalize-space(@class), ' '), ' numbers_title ')]]");

			if(resultNodes == null)
				return new List<AwardLine>();

			var awardLines = new List<AwardLine>();
			foreach(var resultNode in resultNodes)
			{
				var title = Clean(resultNode.SelectSingleNode(
					".//div[contains(concat(' ', normalize-space(@class), ' '), ' numbers_title ')]")?.InnerText);
				if(!configuredLotteries.TryGetValue(Normalize(title), out var configuredLottery))
					continue;

				var number = Clean(resultNode.SelectSingleNode(
					".//*[contains(concat(' ', normalize-space(@class), ' '), ' bbb1 ')]")?.InnerText);
				if(!TwoDigits.IsMatch(number))
					continue;

				var bustedValue = Clean(resultNode.SelectSingleNode(
					".//*[contains(concat(' ', normalize-space(@class), ' '), ' bbb5 ')]")?.InnerText).ToUpperInvariant();
				var description = orderToName.TryGetValue(configuredLottery.Order, out var lotteryName)
					? lotteryName
					: string.Empty;
				var awardLine = CreateAwardLine(
					configuredLottery.Order,
					description,
					number,
					Constants.BustedList.Contains(bustedValue),
					papers);

				if(awardLine != null)
					awardLines.Add(awardLine);
			}

			return awardLines
				.GroupBy(x => x.Order)
				.Select(x => x.First())
				.ToList();
		}

		private List<AwardLine> ProcessOfficialHtml(
			string htmlContent,
			List<ScrapingLottery> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers)
		{
			var hourToLottery = scrapingLotteries
				.Where(IsHonduranLottery)
				.GroupBy(x => NormalizeHour(x.Hour))
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
				var headingMatch = OfficialDrawHeading.Match(textLines[index]);
				if(!headingMatch.Success)
					continue;

				var hour = $"{int.Parse(headingMatch.Groups[1].Value)}:00 {headingMatch.Groups[2].Value.ToUpperInvariant()}M";
				if(!hourToLottery.TryGetValue(hour, out var configuredLottery))
					continue;

				var number = FindOfficialNumber(textLines, index + 1);
				if(string.IsNullOrWhiteSpace(number))
					continue;

				var description = orderToName.TryGetValue(configuredLottery.Order, out var lotteryName)
					? lotteryName
					: string.Empty;
				var awardLine = CreateAwardLine(configuredLottery.Order, description, number, false, papers);
				if(awardLine != null)
					awardLines.Add(awardLine);
			}

			return awardLines;
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

		private static string FindOfficialNumber(List<string> textLines, int startIndex)
		{
			var digits = new List<string>();

			for(var index = startIndex; index < textLines.Count; index++)
			{
				if(OfficialDrawHeading.IsMatch(textLines[index]))
					return string.Empty;

				var numberMatch = OfficialDrawNumber.Match(textLines[index]);
				if(numberMatch.Success)
					return $"{numberMatch.Groups[1].Value}{numberMatch.Groups[2].Value}";

				if(Regex.IsMatch(textLines[index], @"^\d$"))
				{
					digits.Add(textLines[index]);
					if(digits.Count == 2)
						return string.Join(string.Empty, digits);
				}
				else if(digits.Count > 0)
				{
					digits.Clear();
				}
			}

			return string.Empty;
		}

		private bool ContainsTodaysResults(HtmlDocument document)
		{
			var title = Clean(document.DocumentNode.SelectSingleNode(
				"//div[contains(concat(' ', normalize-space(@class), ' '), ' lotto_title ')]//b")?.InnerText);
			var dateText = title.Split('-', 2)[0].Trim();

			var parsed = DateTime.TryParse(dateText, HondurasCulture, DateTimeStyles.AllowWhiteSpaces, out var resultDate)
				|| DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out resultDate);

			return parsed && resultDate.Date == _timeProvider.GetLocalNow().Date;
		}

		private static bool IsHonduranLottery(ScrapingLottery lottery)
		{
			return lottery.Type.Equals("HONDURAS", StringComparison.OrdinalIgnoreCase)
				|| (string.IsNullOrWhiteSpace(lottery.Type)
					&& lottery.Name.Contains("Diaria", StringComparison.OrdinalIgnoreCase));
		}

		private static string Normalize(string value)
		{
			return Clean(value).ToUpperInvariant();
		}

		private static string NormalizeHour(string hour)
		{
			return hour.Trim().ToUpperInvariant();
		}

		private static string Clean(string? value)
		{
			return Regex.Replace(HtmlEntity.DeEntitize(value ?? string.Empty).Replace('\u00A0', ' '), @"\s+", " ").Trim();
		}
	}
}
