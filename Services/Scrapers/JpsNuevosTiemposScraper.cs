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
			var doc = new HtmlDocument();
			doc.LoadHtml(htmlContent);

			var awardLines = new List<AwardLine>();
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

							var value = cells[1]?.InnerText.Trim() ?? "";
							if (!string.IsNullOrEmpty(value) && value != "--")
							{
								papers = papers.Where(x => x.Lottery == description && x.DrawDate.ToShortDateString() == DateTime.Today.ToShortDateString() && x.Numbers.Any(x => x.Value == value)).ToList();
								var amount = papers.Sum(x => x.Numbers.Sum(n => n.Value == value ? n.Amount : 0));
								var busted = papers.Sum(x => x.Numbers.Sum(n => n.Value == value ? n.Busted : 0));

								var isBusted = Constants.BustedList.Contains(cells[2]?.InnerText.Trim() ?? "");
								var award = isBusted ? 85 * amount + 200 * busted : 85 * amount;

								var awardLine = new AwardLine
								{
									Order = order,
									Description = description,
									Number = value,
									Amount = amount,
									Busted = busted,
									Award = award,
									IsBusted = isBusted
								};
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
