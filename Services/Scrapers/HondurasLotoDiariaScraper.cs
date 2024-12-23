using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public class HondurasLotoDiariaScraper: BaseScraper
	{
		public HondurasLotoDiariaScraper(HttpClient httpClient)
			: base(httpClient, "https://www.yelu.hn/lottery/results/la-diaria")
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

			var lotteryNodes = doc.DocumentNode.SelectNodes("//div[@class='lotto_numbers']/div[@class='lotto_numbers']");

			if(lotteryNodes != null)
			{
				foreach(var lotteryNode in lotteryNodes)
				{
					var titleNode = lotteryNode.SelectSingleNode(".//div[@class='numbers_title']");

					if(titleNode != null)
					{
						var order = scrapingLotteries.First(x => x.Name == titleNode?.InnerText.Trim()).Order;
						var description = lotteries.First(x => x.Order == order).Name;

						var numberNode = lotteryNode.SelectSingleNode(".//div[contains(@class, 'lotto_no_r bbb1')]");
						var bustedNode = lotteryNode.SelectSingleNode(".//div[contains(@class, 'lotto_no_r bbb5')]");
						if(numberNode != null && bustedNode != null)
						{
							var number = numberNode.InnerText.Trim();
							var bustedValue = bustedNode.InnerText.Trim();

							papers = papers.Where(x => x.Lottery == description && x.DrawDate.ToShortDateString() == DateTime.Today.ToShortDateString() && x.Numbers.Any(x => x.Value == number)).ToList();

							var isBusted = Constants.BustedList.Contains(bustedValue);
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