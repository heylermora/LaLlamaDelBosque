using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public class DominicanaLaPrimeraScraper: MultiSourceScraper
	{
		private const string LaPrimeraOfficialUrl = "https://laprimera.do/";
		private const string LaPrimeraApiUrl = "https://laprimera.do/wp-admin/admin-ajax.php";
		private const string ApiUserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.5 Mobile/15E148 Safari/604.1";
		private const string EnLoteriaUrl = "https://enloteria.com/loterias/la-primera";
		private static readonly Regex TwoDigits = new(@"^\d{2}$", RegexOptions.Compiled);
		private static readonly IReadOnlyList<ScrapingSource> Sources = new[]
		{
			new ScrapingSource(LaPrimeraApiUrl, LaPrimeraOfficialUrl),
			new ScrapingSource(EnLoteriaUrl, "https://enloteria.com/")
		};

		public DominicanaLaPrimeraScraper(HttpClient httpClient, TimeProvider timeProvider)
			: base(httpClient, Sources, timeProvider)
		{
		}

		protected override IEnumerable<int> GetExpectedOrders(List<ScrapingLottery> scrapingLotteries)
		{
			return scrapingLotteries
				.Where(x => IsLaPrimera(x) && IsDrawAvailable(x))
				.Select(x => x.Order)
				.Distinct();
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
			if(source.Url == LaPrimeraApiUrl)
				htmlContent = ExtractApiPayload(htmlContent);

			var drawToLottery = scrapingLotteries
				.Where(x => IsLaPrimera(x) && IsDrawAvailable(x))
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

		protected override async Task<string> DownloadSource(ScrapingSource source)
		{
			if(source.Url != LaPrimeraApiUrl)
				return await base.DownloadSource(source);

			var nonce = await DownloadNonce(source.Timeout);
			using var content = new MultipartFormDataContent
			{
				{ new StringContent("get_lotteries_results"), "action" },
				{ new StringContent(nonce), "nonce" },
				{ new StringContent(_timeProvider.GetLocalNow().Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), "date" }
			};
			using var request = new HttpRequestMessage(HttpMethod.Post, LaPrimeraApiUrl) { Content = content };
			request.Headers.Referrer = new Uri(LaPrimeraOfficialUrl);
			request.Headers.UserAgent.ParseAdd(ApiUserAgent);
			using var timeout = new CancellationTokenSource(source.Timeout);
			using var response = await _httpClient.SendAsync(request, timeout.Token);
			response.EnsureSuccessStatusCode();
			return await response.Content.ReadAsStringAsync(timeout.Token);
		}

		private async Task<string> DownloadNonce(TimeSpan requestTimeout)
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, LaPrimeraOfficialUrl);
			request.Headers.Referrer = new Uri(LaPrimeraOfficialUrl);
			request.Headers.UserAgent.ParseAdd(ApiUserAgent);
			using var timeout = new CancellationTokenSource(requestTimeout);
			using var response = await _httpClient.SendAsync(request, timeout.Token);
			response.EnsureSuccessStatusCode();
			var homepage = await response.Content.ReadAsStringAsync(timeout.Token);
			var nonceMatch = Regex.Match(homepage, @"(?:data-)?nonce[""']?\s*(?:=|:)\s*[""'](?<nonce>[a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
			if(!nonceMatch.Success)
				throw new InvalidOperationException("La página oficial de La Primera no publicó el nonce requerido por su API.");

			return nonceMatch.Groups["nonce"].Value;
		}

		private static string ExtractApiPayload(string apiResponse)
		{
			try
			{
				using var document = JsonDocument.Parse(apiResponse);
				var values = new List<string>();
				CollectJsonStrings(document.RootElement, values);
				return string.Join(Environment.NewLine, values);
			}
			catch(JsonException)
			{
				return apiResponse;
			}
		}

		private static void CollectJsonStrings(JsonElement element, List<string> values)
		{
			if(element.ValueKind == JsonValueKind.String)
			{
				values.Add(element.GetString() ?? string.Empty);
				return;
			}

			if(element.ValueKind == JsonValueKind.Object)
			{
				foreach(var property in element.EnumerateObject())
					CollectJsonStrings(property.Value, values);
			}
			else if(element.ValueKind == JsonValueKind.Array)
			{
				foreach(var child in element.EnumerateArray())
					CollectJsonStrings(child, values);
			}
		}

		private static List<string> ExtractTextLines(HtmlDocument document)
		{
			return document.DocumentNode
				.Descendants()
				.Where(x => !x.HasChildNodes && !x.Ancestors("script").Any() && !x.Ancestors("style").Any())
				.SelectMany(x => HtmlEntity.DeEntitize(x.InnerText ?? string.Empty)
					.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
				.Select(Clean)
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.ToList();
		}

		private string FindNextNumber(List<string> textLines, int startIndex)
		{
			var number = string.Empty;
			DateTime? resultDate = null;

			for(var index = startIndex; index < textLines.Count && index < startIndex + 30; index++)
			{
				if(!string.IsNullOrWhiteSpace(GetDrawKey(textLines[index])))
					break;

				if(TryParseResultDate(textLines[index], out var parsedDate))
				{
					resultDate = parsedDate.Date;
					continue;
				}

				if(!string.IsNullOrWhiteSpace(number))
					continue;

				if(TwoDigits.IsMatch(textLines[index]))
				{
					number = textLines[index];
					continue;
				}
				if(Regex.IsMatch(textLines[index], @"^\d{1,2}:\d{2}\s*[ap]\.?\s*m\.?$", RegexOptions.IgnoreCase))
					continue;

				var twoDigitNumber = Regex.Match(textLines[index], @"\b\d{2}\b");
				if(twoDigitNumber.Success)
				{
					number = twoDigitNumber.Value;
					continue;
				}

				var separatedDigits = Regex.Match(textLines[index], @"(?:^|\D)(\d)\s+(\d)(?:\s+\d)?(?:\D|$)");
				if(separatedDigits.Success)
					number = $"{separatedDigits.Groups[1].Value}{separatedDigits.Groups[2].Value}";
			}

			return resultDate.HasValue && resultDate.Value != _timeProvider.GetLocalNow().Date
				? string.Empty
				: number;
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

		private static bool TryParseResultDate(string value, out DateTime resultDate)
		{
			resultDate = default;
			var dateMatch = Regex.Match(value, @"\b(?:\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4}|\d{1,2}\s+de\s+[a-záéíóúñ]+\s+(?:de\s+)?\d{4})\b", RegexOptions.IgnoreCase);
			return dateMatch.Success
				&& DateTime.TryParse(dateMatch.Value, CultureInfo.GetCultureInfo("es-DO"), DateTimeStyles.AllowWhiteSpaces, out resultDate);
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
