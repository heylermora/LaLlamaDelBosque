using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Services.Scrapers;
using LaLlamaDelBosque.Utils;
using System.Text.RegularExpressions;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public class NicaraguaLotoDiariaScraper: BaseScraper
	{
		public NicaraguaLotoDiariaScraper(HttpClient httpClient)
			: base(httpClient, "https://www.tiemposnica.com/")
		{
		}

		protected override List<AwardLine> ProcessHtml(string htmlContent, List<ScrapingLottery> scrapingLotteries, List<Lottery> lotteries, List<Paper> papers)
		{
			var filteredLotteries = scrapingLotteries.Where(x => x.Type == "NICA").ToList();
			var doc = new HtmlDocument();
			doc.LoadHtml(htmlContent);

			var awardLines = new List<AwardLine>();

			var contentInnerNode = doc.DocumentNode.Descendants("div")
												   .FirstOrDefault(node => node.GetAttributeValue("class", "").Contains("content_inner clr"));

			if(contentInnerNode != null)
			{
				var results = contentInnerNode.Descendants("div")
										.Where(node => node.GetAttributeValue("class", "").Contains("draw_results"))
										.ToList();

				if(results != null)
				{
					foreach(var result in results)
					{
						var dateNode = result.SelectSingleNode(".//div[@class='draw_content']//ul[@class='list list_inline']/li");
						if(dateNode == null || !DateTime.TryParse(dateNode.InnerText.Trim(), out var extractedDate) || extractedDate.Date != DateTime.Today)
							continue;

						var lotteryText = result.SelectSingleNode(".//h3[@class='number_heading']//span[@class='optional']")?.InnerText.Trim() ?? "";
						var timeText = result.SelectSingleNode(".//div[@class='logo_lockup']//p[@class='draw_date']")?.InnerText.Trim() ?? "";

						var lottery = filteredLotteries.FirstOrDefault(x => x.Name == lotteryText && x.Hour == timeText);
						if(lottery == null)
							continue;

						var order = lottery.Order;
						var description = lotteries.FirstOrDefault(x => x.Order == order)?.Name ?? "";

						var numberNodes = result.SelectNodes(".//ul[@class='draw_numbers']//li[@class='number main']");
						if(numberNodes == null || !numberNodes.Any())
							continue;

						var value = string.Join("", numberNodes.Select(node => node.InnerText.Trim()));
						if(string.IsNullOrEmpty(value))
							continue;

						var awardLine = CreateAwardLine(order, description, value.Trim(), false, papers);
						if(awardLine != null)
							awardLines.Add(awardLine);
					}
				}
			}

			return awardLines;
		}
	}
}
