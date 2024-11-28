using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Services.Scrapers;
using LaLlamaDelBosque.Utils;

namespace LaLlamaDelBosque.Services.NewFolder.Scrapers
{
	public class JpsNuevosTiemposScraper: BaseScraper
	{
		public JpsNuevosTiemposScraper(HttpClient httpClient)
			: base(httpClient, "https://www.jpsloteria.com/loteria-resultados")
		{
		}

		protected override List<AwardLine> ProcessHtml(string htmlContent, List<ScrapingLottery> scrapingLotteries, List<Lottery> lotteries, List<Paper> papers)
		{
			var awardLines = new List<AwardLine>();

			var doc = new HtmlDocument();
			doc.LoadHtml(htmlContent);

			var extractedDate = DateTime.Parse(doc.DocumentNode.SelectSingleNode("//time[@datetime]")?.GetAttributeValue("datetime", "")
					?? throw new InvalidOperationException("No se pudo extraer la fecha."));

			if(extractedDate.Date != DateTime.Today)
				return awardLines;

			var extractedData = new List<(int Order, string Description, string Number, bool IsBusted)>();

			var tableNode = doc.DocumentNode.SelectSingleNode("//table[contains(@class, 'numeros-md-mov numeros-gr-esc')]");

			if(tableNode != null)
			{
				var rowNodes = tableNode.SelectNodes(".//tr | .//td[span[contains(text(), 'Noche')]]");

				if(rowNodes != null)
				{
					foreach(var row in rowNodes)
					{
						var cells = row.Name == "tr"
									   ? row.SelectNodes(".//td")
									   : new HtmlNodeCollection(row.ParentNode) { row, row.NextSibling, row.NextSibling?.NextSibling, row.NextSibling?.NextSibling?.NextSibling };

						if(cells != null && cells.Count == 4)
						{
							var order = scrapingLotteries.First(x => x.Name == cells[0]?.InnerText.Trim()).Order;
							var description = lotteries.First(x => x.Order == order).Name;
							var number = cells[1]?.InnerText.Trim() ?? "";
							if(string.IsNullOrEmpty(number) || number == "--") continue;

							var isBusted = Constants.BustedList.Contains(cells[2]?.InnerText.Trim() ?? "");
							var awardLine = CreateAwardLine(order, description, number, isBusted, papers);
							if(awardLine != null)
							{
								awardLines.Add(awardLine);
							}
						}
					}
				}
			}
			return awardLines;
		}
	}
}
