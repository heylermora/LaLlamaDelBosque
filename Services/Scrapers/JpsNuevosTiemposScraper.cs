using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Interfaces;
using System.Text.RegularExpressions;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public class JpsNuevosTiemposScraper: MultiSourceScraper
	{
		public override string LotteryType => "TICA";
		private static readonly Regex HourLine = new(@"^(?:Sorteo\s*)?(\d{1,2})(?::(\d{2}))?\s*(?:([AP])\.?\s*M\.?)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex TwoDigits = new(@"^\d{2}$", RegexOptions.Compiled);

		public JpsNuevosTiemposScraper(HttpClient httpClient, TimeProvider timeProvider, IJsonRepository repository)
			: base(httpClient, ScrapingSourceCatalog.GetEnabled(repository, "TICA"), timeProvider)
		{
		}

		protected override IEnumerable<int> GetExpectedOrders(List<ScrapingDrawConfiguration> scrapingLotteries)
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
			List<ScrapingDrawConfiguration> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers,
			ScrapingSource source)
		{
			var awardLines = new List<AwardLine>();
			var sourceKeyToLottery = scrapingLotteries
				.Where(HasJpsSourceKey)
				.GroupBy(x => NormalizeSourceKey(x.SourceKey))
				.ToDictionary(x => x.Key, x => x.First());
			var configuredAliases = BuildConfiguredAliases(scrapingLotteries);

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
				var sourceKey = GetSourceKey(textLines[index], configuredAliases);
				if(string.IsNullOrWhiteSpace(sourceKey))
					continue;

				if(!sourceKeyToLottery.TryGetValue(sourceKey, out var scrapingLottery))
					continue;

				if(!source.IsDedicatedCostaRicaPage && !isOfficialJpsPage && !IsCostaRicaDraw(textLines, index, sourceKey, configuredAliases))
					continue;

				var number = FindNextNumber(textLines, index + 1, sourceKey, configuredAliases);
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

		private static bool HasJpsSourceKey(ScrapingDrawConfiguration scrapingLottery)
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
				(7, 0, "P") => "noche",
				(7, 30, "P") => "noche",
				(13, 0, "") => "manana",
				(16, 30, "") => "tarde",
				(19, 0, "") => "noche",
				(19, 30, "") => "noche",
				_ => string.Empty
			};
		}

		private static string GetSourceKey(string textLine, IReadOnlyDictionary<string, string>? configuredAliases = null)
		{
			var configuredValue = NormalizeAlias(textLine);
			if(configuredAliases != null && configuredAliases.TryGetValue(configuredValue, out var configuredSourceKey))
				return configuredSourceKey;

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

		private static bool IsCostaRicaDraw(
			List<string> textLines,
			int startIndex,
			string expectedSourceKey,
			IReadOnlyDictionary<string, string> configuredAliases)
		{
			for(var index = startIndex; index < textLines.Count; index++)
			{
				if(textLines[index].Contains("Nuevos Tiempos", StringComparison.OrdinalIgnoreCase)
					|| textLines[index].Contains("Costa Rica", StringComparison.OrdinalIgnoreCase)
					|| textLines[index].Equals("CR", StringComparison.OrdinalIgnoreCase))
					return true;

				var encounteredSourceKey = GetSourceKey(textLines[index], configuredAliases);
				if(!string.IsNullOrWhiteSpace(encounteredSourceKey))
				{
					if(encounteredSourceKey.Equals(expectedSourceKey, StringComparison.Ordinal))
						continue;

					return false;
				}

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

		private static string FindNextNumber(
			List<string> textLines,
			int startIndex,
			string expectedSourceKey,
			IReadOnlyDictionary<string, string> configuredAliases)
		{
			for(var index = startIndex; index < textLines.Count; index++)
			{
				var encounteredSourceKey = GetSourceKey(textLines[index], configuredAliases);
				if(!string.IsNullOrWhiteSpace(encounteredSourceKey))
				{
					if(!encounteredSourceKey.Equals(expectedSourceKey, StringComparison.Ordinal))
						return string.Empty;

					// Algunas fuentes repiten "Noche" y luego "7:30 PM" antes del número.
					// Ambos encabezados pertenecen al mismo sorteo y no deben cortar la búsqueda.
					continue;
				}

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

		private static IReadOnlyDictionary<string, string> BuildConfiguredAliases(IEnumerable<ScrapingDrawConfiguration> draws)
		{
			return draws
				.SelectMany(draw => draw.ScrapingNames.Concat(draw.ScrapingHours)
					.Select(alias => new { Alias = NormalizeAlias(alias), draw.SourceKey }))
				.Where(x => !string.IsNullOrWhiteSpace(x.Alias) && !string.IsNullOrWhiteSpace(x.SourceKey))
				.GroupBy(x => x.Alias)
				.Where(x => x.Select(value => value.SourceKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
				.ToDictionary(x => x.Key, x => NormalizeSourceKey(x.First().SourceKey));
		}

		private static string NormalizeAlias(string value)
		{
			return Clean(value)
				.ToLowerInvariant()
				.Replace("ñ", "n")
				.Replace("í", "i");
		}
	}
}
