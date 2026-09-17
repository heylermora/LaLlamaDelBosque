using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using System.Text.RegularExpressions;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public class JpsNuevosTiemposScraper: MultiSourceScraper
	{
		private const string OfficialUrl = "https://www.jps.go.cr/resultados/nuevos-tiempos-reventados";
		private const string CostaRicaResultsUrl = "https://loteriacostarica.org/nuevos-tiempos.php?periodo=3";
		private const string NicaTiemposUrl = "https://nicatiempos.com/";
		private static readonly Regex HourLine = new(@"^(?:Sorteo\s*)?(\d{1,2})(?::(\d{2}))?\s*([AP])\.?\s*M\.?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex TwoDigits = new(@"^\d{2}$", RegexOptions.Compiled);

		private static readonly IReadOnlyList<ScrapingSource> Sources = new[]
		{
			new ScrapingSource(OfficialUrl, "https://www.jps.go.cr/", true),
			new ScrapingSource(CostaRicaResultsUrl, "https://loteriacostarica.org/", true),
			new ScrapingSource(NicaTiemposUrl, "https://nicatiempos.com/")
		};

		public JpsNuevosTiemposScraper(HttpClient httpClient, TimeProvider timeProvider)
			: base(httpClient, Sources, timeProvider)
		{
		}

		protected override IEnumerable<int> GetExpectedOrders(List<ScrapingLottery> scrapingLotteries)
		{
			return scrapingLotteries
				.Where(HasJpsSourceKey)
				.Select(x => x.Order)
				.Distinct();
		}

		protected override string GetAllSourcesFailedMessage()
		{
			return "No fue posible obtener los resultados de Nuevos Tiempos desde ninguna de las fuentes configuradas.";
		}

		protected override List<AwardLine> ProcessHtml(
			string htmlContent,
			List<ScrapingLottery> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers,
			ScrapingSource source)
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
			var isOfficialJpsPage = textLines.Any(x =>
				x.Contains("Junta de Protección Social", StringComparison.OrdinalIgnoreCase)
				|| x.Contains("Junta de Proteccion Social", StringComparison.OrdinalIgnoreCase));

			for(var index = 0; index < textLines.Count; index++)
			{
				var sourceKey = GetSourceKey(textLines[index]);
				if(string.IsNullOrWhiteSpace(sourceKey))
					continue;

				if(!sourceKeyToLottery.TryGetValue(sourceKey, out var scrapingLottery))
					continue;

				if(!source.IsDedicatedCostaRicaPage && !isOfficialJpsPage && !IsCostaRicaDraw(textLines, index + 1))
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
			if(!scrapingLottery.Type.Equals("TICA", StringComparison.OrdinalIgnoreCase))
				return false;

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

		private static string GetSourceKey(string textLine)
		{
			var hourMatch = HourLine.Match(textLine);
			if(hourMatch.Success)
				return ToSourceKey(hourMatch);

			var normalized = textLine
				.ToLowerInvariant()
				.Replace("ñ", "n")
				.Replace("í", "i");
			var isDrawHeading = normalized.Contains("nuevos tiempos", StringComparison.Ordinal)
				|| normalized is "manana" or "mediodia" or "medio dia" or "tarde" or "noche";

			if(!isDrawHeading)
				return string.Empty;

			if(normalized.Contains("manana", StringComparison.Ordinal)
				|| normalized.Contains("mediodia", StringComparison.Ordinal)
				|| normalized.Contains("medio dia", StringComparison.Ordinal))
				return "manana";
			if(normalized.Contains("tarde", StringComparison.Ordinal))
				return "tarde";
			if(normalized.Contains("noche", StringComparison.Ordinal))
				return "noche";

			return string.Empty;
		}

		private static bool IsCostaRicaDraw(List<string> textLines, int startIndex)
		{
			for(var index = startIndex; index < textLines.Count; index++)
			{
				if(!string.IsNullOrWhiteSpace(GetSourceKey(textLines[index])))
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
				.Where(x => !x.HasChildNodes && !x.Ancestors("script").Any() && !x.Ancestors("style").Any())
				.Select(x => Clean(x.InnerText))
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.ToList();
		}

		private static string FindNextNumber(List<string> textLines, int startIndex)
		{
			for(var index = startIndex; index < textLines.Count; index++)
			{
				if(!string.IsNullOrWhiteSpace(GetSourceKey(textLines[index])))
					return string.Empty;

				if(TwoDigits.IsMatch(textLines[index]))
					return textLines[index];

				if(Regex.IsMatch(textLines[index], @"\b\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4}\b"))
					continue;

				var candidate = Regex.Match(textLines[index], @"\b\d{2}\b");
				if(candidate.Success)
					return candidate.Value;
			}

			return string.Empty;
		}

		private static string Clean(string? value)
		{
			return Regex.Replace(HtmlEntity.DeEntitize(value ?? string.Empty).Replace('\u00A0', ' '), @"\s+", " ").Trim();
		}
	}
}
