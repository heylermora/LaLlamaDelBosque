using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Services.Scrapers;
using LaLlamaDelBosque.Utils;

namespace LaLlamaDelBosque.Services.NewFolder.Scrapers
{
	public class HondurasLotoDiariaScraper: BaseScraper
	{
		public HondurasLotoDiariaScraper(HttpClient httpClient)
			: base(httpClient, "https://www.yelu.hn/lottery/results/la-diaria")
		{
		}

		protected override List<AwardLine> ProcessHtml(string htmlContent, List<ScrapingLottery> scrapingLotteries, List<Lottery> lotteries, List<Paper> papers)
		{
			var doc = new HtmlDocument();
			doc.LoadHtml(htmlContent);

			var awardLines = new List<AwardLine>();
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

						var resultNode = lotteryNode.SelectSingleNode(".//div[contains(@class, 'lotto_no_r bbb1')]");
						var bustedNode = lotteryNode.SelectSingleNode(".//div[contains(@class, 'lotto_no_r bbb5')]");
						if(resultNode != null && bustedNode != null)
						{
							var amountValue = resultNode.InnerText.Trim();
							var bustedValue = bustedNode.InnerText.Trim();

							papers = papers.Where(x => x.Lottery == description && x.DrawDate.ToShortDateString() == DateTime.Today.ToShortDateString() && x.Numbers.Any(x => x.Value == amountValue)).ToList();
							var amount = papers.Sum(x => x.Numbers.Sum(n => n.Value == amountValue ? n.Amount : 0));
							var busted = papers.Sum(x => x.Numbers.Sum(n => n.Value == amountValue ? n.Busted : 0));

							var isBusted = Constants.BustedList.Contains(bustedValue);
							var award = isBusted ? 85 * amount + 200 * busted : 85 * amount;
							var awardLine = new AwardLine
							{
								Order = order,
								Description = description,
								Number = amountValue,
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
			return awardLines;
		}
	}
}