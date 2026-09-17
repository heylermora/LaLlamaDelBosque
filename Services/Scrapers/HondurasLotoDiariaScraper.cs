using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public class HondurasLotoDiariaScraper: MultiSourceScraper
	{
		private const string YeluResultsUrl = "https://www.yelu.hn/lottery/results/la-diaria";
		private const string OfficialResultsUrl = "https://loto.hn/?pag=diaria";
		private const string OfficialApiUrl = "https://loto.hn/api/resultados_diaria_por_fecha.php";
		private const string ApiUserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.5 Mobile/15E148 Safari/604.1";
		private static readonly CultureInfo HondurasCulture = CultureInfo.GetCultureInfo("es-HN");
		private static readonly Regex TwoDigits = new(@"^\d{2}$", RegexOptions.Compiled);
		private static readonly Regex OfficialDrawHeading = new(@"^(?:SORTEO\s+)?(\d{1,2}):00\s*([AP])\.?\s*M\.?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly IReadOnlyList<ScrapingSource> Sources = new[]
		{
			new ScrapingSource(OfficialApiUrl, OfficialResultsUrl),
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
			if(source.Url == OfficialApiUrl)
				return ProcessOfficialApi(htmlContent, scrapingLotteries, lotteries, papers);

			return source.Url == YeluResultsUrl
				? ProcessYeluHtml(htmlContent, scrapingLotteries, lotteries, papers)
				: ProcessOfficialHtml(htmlContent, scrapingLotteries, lotteries, papers);
		}

		protected override async Task<string> DownloadSource(ScrapingSource source)
		{
			if(source.Url != OfficialApiUrl)
				return await base.DownloadSource(source);

			var date = _timeProvider.GetLocalNow().Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
			using var request = new HttpRequestMessage(HttpMethod.Get, $"{OfficialApiUrl}?fecha={date}");
			request.Headers.Referrer = new Uri(OfficialResultsUrl);
			request.Headers.UserAgent.ParseAdd(ApiUserAgent);
			using var timeout = new CancellationTokenSource(source.Timeout);
			using var response = await _httpClient.SendAsync(request, timeout.Token);
			response.EnsureSuccessStatusCode();
			return await response.Content.ReadAsStringAsync(timeout.Token);
		}

		private List<AwardLine> ProcessOfficialApi(
			string jsonContent,
			List<ScrapingLottery> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers)
		{
			JsonDocument document;
			try
			{
				document = JsonDocument.Parse(jsonContent);
			}
			catch(JsonException)
			{
				return ProcessOfficialHtml(jsonContent, scrapingLotteries, lotteries, papers, false);
			}

			using(document)
			{
				var objects = new List<JsonElement>();
				CollectJsonObjects(document.RootElement, objects);
				var apiResults = new Dictionary<string, string>();

				foreach(var item in objects)
				{
					var values = item.EnumerateObject()
						.Where(x => x.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
						.Select(x => (Name: Normalize(x.Name), Value: Clean(x.Value.ToString())))
						.ToList();
					var hour = GetApiHour(values.Select(x => x.Value));
					if(string.IsNullOrWhiteSpace(hour))
						continue;

					var numberValues = values
						.Where(x => (x.Name.Contains("NUM", StringComparison.Ordinal) || x.Name.Contains("RESULT", StringComparison.Ordinal))
							&& !x.Name.Contains("EXTRA", StringComparison.Ordinal) && !x.Name.Contains("MAS", StringComparison.Ordinal))
						.Select(x => x.Value)
						.ToList();
					var number = GetTwoDigitNumber(numberValues);
					if(!string.IsNullOrWhiteSpace(number))
						apiResults.TryAdd(hour, number);
				}

				var flattenedValues = new List<(string Name, string Value)>();
				CollectJsonValues(document.RootElement, string.Empty, flattenedValues);
				AddResultFromIdPrefix(apiResults, flattenedValues, "num11", "11:00 AM");
				AddResultFromIdPrefix(apiResults, flattenedValues, "num15", "3:00 PM");
				AddResultFromIdPrefix(apiResults, flattenedValues, "num21", "9:00 PM");
				AddResultFromHourContext(apiResults, flattenedValues, "11", "11:00 AM");
				AddResultFromHourContext(apiResults, flattenedValues, "15", "3:00 PM");
				AddResultFromHourContext(apiResults, flattenedValues, "3", "3:00 PM");
				AddResultFromHourContext(apiResults, flattenedValues, "21", "9:00 PM");
				AddResultFromHourContext(apiResults, flattenedValues, "9", "9:00 PM");

				return CreateHondurasAwardLines(apiResults, scrapingLotteries, lotteries, papers);
			}
		}

		private static string GetApiHour(IEnumerable<string> values)
		{
			foreach(var value in values)
			{
				var headingMatch = OfficialDrawHeading.Match(value);
				if(headingMatch.Success)
					return $"{int.Parse(headingMatch.Groups[1].Value)}:00 {headingMatch.Groups[2].Value.ToUpperInvariant()}M";

				var twentyFourHourMatch = Regex.Match(value, @"^(11|15|21)(?::00(?::00)?)?$");
				if(twentyFourHourMatch.Success)
				{
					return twentyFourHourMatch.Groups[1].Value switch
					{
						"11" => "11:00 AM",
						"15" => "3:00 PM",
						"21" => "9:00 PM",
						_ => string.Empty
					};
				}
			}

			return string.Empty;
		}

		private static void AddResultFromIdPrefix(
			Dictionary<string, string> resultsByHour,
			List<(string Name, string Value)> values,
			string prefix,
			string hour)
		{
			if(resultsByHour.ContainsKey(hour))
				return;

			var digits = values
				.Where(x => x.Name.Contains(prefix, StringComparison.OrdinalIgnoreCase)
					&& !x.Name.Contains("extra", StringComparison.OrdinalIgnoreCase))
				.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
				.Select(x => x.Value)
				.ToList();
			var number = GetTwoDigitNumber(digits);
			if(!string.IsNullOrWhiteSpace(number))
				resultsByHour.Add(hour, number);
		}

		private static void AddResultFromHourContext(
			Dictionary<string, string> resultsByHour,
			List<(string Name, string Value)> values,
			string hourToken,
			string hour)
		{
			if(resultsByHour.ContainsKey(hour))
				return;

			var contextPattern = $@"(?:^|[.\[]){Regex.Escape(hourToken)}(?:am|pm)?(?:[.\]]|$)";
			var candidates = values
				.Where(x => Regex.IsMatch(x.Name, contextPattern, RegexOptions.IgnoreCase)
					&& !Regex.IsMatch(x.Name, @"(?:extra|mas|num(?:ero)?3|_3)(?:\D|$)", RegexOptions.IgnoreCase))
				.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
				.Select(x => x.Value)
				.ToList();
			var number = GetTwoDigitNumber(candidates);
			if(!string.IsNullOrWhiteSpace(number))
				resultsByHour.Add(hour, number);
		}

		private List<AwardLine> CreateHondurasAwardLines(
			Dictionary<string, string> resultsByHour,
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
			var awardLines = new List<AwardLine>();

			foreach(var result in resultsByHour)
			{
				if(!hourToLottery.TryGetValue(result.Key, out var configuredLottery))
					continue;

				var description = orderToName.TryGetValue(configuredLottery.Order, out var lotteryName)
					? lotteryName
					: string.Empty;
				var awardLine = CreateAwardLine(configuredLottery.Order, description, result.Value, false, papers);
				if(awardLine != null)
					awardLines.Add(awardLine);
			}

			return awardLines;
		}

		private static string GetTwoDigitNumber(List<string> values)
		{
			var completeNumber = values.FirstOrDefault(x => Regex.IsMatch(x, @"^\d{2,3}$"));
			if(!string.IsNullOrWhiteSpace(completeNumber))
				return completeNumber[..2];

			var digits = values.Where(x => Regex.IsMatch(x, @"^\d$")).Take(2).ToList();
			return digits.Count == 2 ? string.Concat(digits) : string.Empty;
		}

		private static void CollectJsonObjects(JsonElement element, List<JsonElement> objects)
		{
			if(element.ValueKind == JsonValueKind.Object)
			{
				objects.Add(element);
				foreach(var property in element.EnumerateObject())
					CollectJsonObjects(property.Value, objects);
			}
			else if(element.ValueKind == JsonValueKind.Array)
			{
				foreach(var child in element.EnumerateArray())
					CollectJsonObjects(child, objects);
			}
		}

		private static void CollectJsonValues(JsonElement element, string path, List<(string Name, string Value)> values)
		{
			if(element.ValueKind == JsonValueKind.Object)
			{
				foreach(var property in element.EnumerateObject())
					CollectJsonValues(property.Value, $"{path}.{property.Name}", values);
			}
			else if(element.ValueKind == JsonValueKind.Array)
			{
				var index = 0;
				foreach(var child in element.EnumerateArray())
					CollectJsonValues(child, $"{path}[{index++}]", values);
			}
			else if(element.ValueKind is JsonValueKind.String or JsonValueKind.Number)
			{
				values.Add((path, Clean(element.ToString())));
			}
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
			List<Paper> papers,
			bool validatePageDate = true)
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

			if(validatePageDate && !ContainsOfficialResultsForToday(document))
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
