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
			var doc = new HtmlDocument();
			doc.LoadHtml(htmlContent);

			var awardLines = new List<AwardLine>();
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
							var value = resultNode.InnerText.Trim();

							papers = papers.Where(x => x.Lottery == description && x.DrawDate.ToShortDateString() == DateTime.Today.ToShortDateString() && x.Numbers.Any(x => x.Value == value)).ToList();
							var amount = papers.Sum(x => x.Numbers.Sum(n => n.Value == value ? n.Amount : 0));
							var busted = papers.Sum(x => x.Numbers.Sum(n => n.Value == value ? n.Busted : 0));

							var awardLine = new AwardLine
							{
								Order = order,
								Description = description,
								Number = value,
								Amount = amount,
								Busted = busted,
								Award = 85 * amount,
								IsBusted = false
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
