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
		private static readonly IReadOnlyList<ScrapingSource> Sources = new[]
		{
			new ScrapingSource(OfficialResultsUrl, "https://loto.hn/"),
			new ScrapingSource(YeluResultsUrl, "https://www.yelu.hn/")
		};

		public HondurasLotoDiariaScraper(HttpClient httpClient, TimeProvider timeProvider)
			: base(httpClient, Sources, timeProvider)
		{
		}

		protected override IEnumerable<int> GetExpectedOrders(List<ScrapingLottery> scrapingLotteries)
		{
			return scrapingLotteries
				.Where(x => IsHonduranLottery(x) && IsDrawAvailable(x))
				.Select(x => x.Order)
				.Distinct();
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
				.Where(x => IsHonduranLottery(x) && IsDrawAvailable(x))
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
				.Where(x => IsHonduranLottery(x) && IsDrawAvailable(x))
				.GroupBy(x => NormalizeHour(x.Hour))
				.ToDictionary(x => x.Key, x => x.First());
			var orderToName = lotteries
				.GroupBy(x => x.Order)
				.ToDictionary(x => x.Key, x => x.First().Name);
			var document = new HtmlDocument();
			document.LoadHtml(htmlContent);

			if(!ContainsOfficialResultsForToday(document))
				return new List<AwardLine>();

			var drawNodes = document.DocumentNode.SelectNodes(
				"//div[contains(concat(' ', normalize-space(@class), ' '), ' sorteo ')][.//h3]");
			if(drawNodes == null)
				return new List<AwardLine>();

			var awardLines = new List<AwardLine>();
			foreach(var drawNode in drawNodes)
			{
				var heading = Clean(drawNode.SelectSingleNode(".//h3")?.InnerText);
				var headingMatch = OfficialDrawHeading.Match(heading);
				if(!headingMatch.Success)
					continue;

				var hour = $"{int.Parse(headingMatch.Groups[1].Value)}:00 {headingMatch.Groups[2].Value.ToUpperInvariant()}M";
				if(!hourToLottery.TryGetValue(hour, out var configuredLottery))
					continue;

				var digitNodes = drawNode.SelectNodes(
					".//span[contains(concat(' ', normalize-space(@class), ' '), ' num ') and not(contains(concat(' ', normalize-space(@class), ' '), ' extra '))]");
				if(digitNodes == null || digitNodes.Count < 2)
					continue;

				var digits = digitNodes.Take(2).Select(x => Clean(x.InnerText)).ToList();
				if(digits.Any(x => !Regex.IsMatch(x, @"^\d$")))
					continue;

				var number = string.Concat(digits);
				var description = orderToName.TryGetValue(configuredLottery.Order, out var lotteryName)
					? lotteryName
					: string.Empty;
				var awardLine = CreateAwardLine(configuredLottery.Order, description, number, false, papers);
				if(awardLine != null)
					awardLines.Add(awardLine);
			}

			return awardLines;
		}

		private bool ContainsOfficialResultsForToday(HtmlDocument document)
		{
			var today = _timeProvider.GetLocalNow().Date;
			var activeDay = Clean(document.DocumentNode.SelectSingleNode(
				"//div[contains(concat(' ', normalize-space(@class), ' '), ' calendario-real ')]//td[contains(concat(' ', normalize-space(@class), ' '), ' activo ')]")?.InnerText);
			var calendarCells = document.DocumentNode.SelectNodes(
				"//div[contains(concat(' ', normalize-space(@class), ' '), ' calendario-real ')]//tbody//td")?.ToList() ?? new List<HtmlNode>();
			var firstDayIndex = calendarCells.FindIndex(x => Clean(x.InnerText) == "1");
			var lastDay = DateTime.DaysInMonth(today.Year, today.Month).ToString(CultureInfo.InvariantCulture);
			var calendarMatchesCurrentMonth = firstDayIndex == (int)new DateTime(today.Year, today.Month, 1).DayOfWeek
				&& calendarCells.Any(x => Clean(x.InnerText) == lastDay);
			var containsCurrentYear = document.DocumentNode.SelectNodes("//select[@id='filtro-ano']/option")?
				.Any(x => Clean(x.InnerText) == today.Year.ToString(CultureInfo.InvariantCulture)) == true;

			return activeDay == today.Day.ToString(CultureInfo.InvariantCulture)
				&& calendarMatchesCurrentMonth
				&& containsCurrentYear;
		}

		private bool IsDrawAvailable(ScrapingLottery lottery)
		{
			return DateTime.TryParseExact(
				lottery.Hour,
				new[] { "h:mm tt", "hh:mm tt" },
				CultureInfo.InvariantCulture,
				DateTimeStyles.AllowWhiteSpaces,
				out var drawTime)
				&& drawTime.TimeOfDay <= _timeProvider.GetLocalNow().TimeOfDay;
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
