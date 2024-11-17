using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Services.Scrapers;
using LaLlamaDelBosque.Utils;

namespace LaLlamaDelBosque.Services.NewFolder.Scrapers
{
	public class NicaraguaLotoDiariaScraper: BaseScraper
	{
		public NicaraguaLotoDiariaScraper(HttpClient httpClient)
			: base(httpClient, "https://nuevaya.com.ni/loto-diaria-de-nicaragua/")
		{
		}

		protected override List<AwardLine> ProcessHtml(string htmlContent, List<ScrapingLottery> scrapingLotteries, List<Lottery> lotteries, List<Paper> papers)
		{
			var doc = new HtmlDocument();
			doc.LoadHtml(htmlContent);

			var awardLines = new List<AwardLine>();
			var figureNode = doc.DocumentNode.SelectSingleNode("//figure");

			if(figureNode != null)
			{
				var rowNodes = figureNode.SelectNodes(".//tr[td and (td[not(.//strong)]/text()[normalize-space() != ''])]");

				if(rowNodes != null)
				{
					foreach(var row in rowNodes)
					{
						var cells = row.SelectNodes(".//td");

						if(cells != null && cells.Count == 3)
						{
							var order = scrapingLotteries.First(x => x.Name == cells[1]?.InnerText.Trim()).Order;
							var description = lotteries.First(x => x.Order == order).Name;

							var values = cells[2]?.InnerText.Trim().Split(" ");
							if(!string.IsNullOrEmpty(values?[0]) && !string.IsNullOrEmpty(values?[1]))
							{
								papers = papers.Where(x => x.Lottery == description && x.DrawDate.ToShortDateString() == DateTime.Today.ToShortDateString() && x.Numbers.Any(x => x.Value == values[0])).ToList();
								var amount = papers.Sum(x => x.Numbers.Sum(n => n.Value == values[0] ? n.Amount : 0));
								var busted = papers.Sum(x => x.Numbers.Sum(n => n.Value == values[0] ? n.Busted : 0));

								var isBusted = Constants.BustedList.Contains(values[1]);
								var award = isBusted ? 85 * amount + 200 * busted : 85 * amount;

								var awardLine = new AwardLine
								{
									Order = order,
									Description = description,
									Number = values[0],
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
