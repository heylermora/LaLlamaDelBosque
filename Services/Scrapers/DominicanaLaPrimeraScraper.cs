using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Services.Scrapers;

namespace LaLlamaDelBosque.Services.NewFolder.Scrapers
{
	public class DominicanaLaPrimeraScraper: BaseScraper
	{
		public DominicanaLaPrimeraScraper(HttpClient httpClient)
			: base(httpClient, "https://www.yelu.do/leidsa/results/la-primera")
		{
		}

		protected override List<AwardLine> ProcessHtml(string htmlContent, List<ScrapingLottery> scrapingLotteries, List<Lottery> lotteries, List<Paper> papers)
		{
			var awardLines = new List<AwardLine>();

			var doc = new HtmlDocument();
			doc.LoadHtml(htmlContent);

			var dateNode = doc.DocumentNode.SelectSingleNode("//div[@class='lotto_title']/b");
			var extractedDate = DateTime.Parse(dateNode?.InnerText.Trim().Split('-')[0]
								?? throw new InvalidOperationException("No se pudo extraer la fecha."));

			if(extractedDate.Date != DateTime.Today)
				return awardLines;

			var lotteryNodes = doc.DocumentNode.SelectNodes("//div[@class='lotto_numbers']")
				.Where(node => node.SelectNodes(".//div[contains(@class, 'lotto_numbers')]") == null
					|| !node.SelectNodes(".//div[contains(@class, 'lotto_numbers')]").Any())
				.ToList();

			if(lotteryNodes != null)
			{
				foreach(var lotteryNode in lotteryNodes)
				{
					var titleNode = lotteryNode.SelectSingleNode(".//div[@class='numbers_title']");

					if(titleNode != null)
					{
						var order = scrapingLotteries.First(x => x.Name == titleNode?.InnerText.Trim()).Order;
						var description = lotteries.First(x => x.Order == order).Name;

						var resultNode = lotteryNode.SelectSingleNode(".//div[contains(@class, 'lotto_no_r bbb1')]");
						if(resultNode != null)
						{
							var number = resultNode.InnerText.Trim();

							var awardLine = CreateAwardLine(order, description, number, false, papers);
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
