using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using System.Text.RegularExpressions;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public class NicaraguaLotoDiariaScraper: BaseScraper
	{
		public NicaraguaLotoDiariaScraper(HttpClient httpClient)
			: base(httpClient, "https://www.yelu.com.ni/lottery/loto-nicaragua-hoy")
		{
		}

		protected override List<AwardLine> ProcessHtml(
			string htmlContent,
			List<ScrapingLottery> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers)
		{
			var awardLines = new List<AwardLine>();
			var nicaLotteries = scrapingLotteries.Where(x => x.Type == "NICA").ToList();

			var doc = new HtmlDocument();
			doc.LoadHtml(htmlContent);

			var draws = doc.DocumentNode.SelectNodes("//div[contains(@class, 'lotto_numbers') and div[contains(@class, 'lotto_no_time')]]");
			if(draws == null) return awardLines;

			foreach(var draw in draws)
			{
				var timeNode = draw.SelectSingleNode(".//div[contains(@class, 'lotto_no_time')]");
				var digit1Node = draw.SelectSingleNode(".//div[contains(@class, 'bbb1')]");
				var digit2Node = draw.SelectSingleNode(".//div[contains(@class, 'bbb2')]");
				var plus1Node = draw.SelectSingleNode(".//div[contains(@class, 'bbb5')]");

				if(timeNode == null || digit1Node == null || digit2Node == null || plus1Node == null)
					continue;

				var timeText = timeNode.InnerText.Trim().ToLower();  // "11am", "3pm", "9pm"
				timeText = timeText.Replace("am", ":00 am").Replace("pm", ":00 pm");

				var matchedLottery = nicaLotteries.FirstOrDefault(l => l.Hour.Replace(".", "").ToLower() == timeText);
				if(matchedLottery == null) continue;

				var numberText = digit1Node.InnerText.Trim() + digit2Node.InnerText.Trim();  // ej: "18"
				if(!Regex.IsMatch(numberText, @"^\d{2}$")) continue;

				var plus1Text = plus1Node.InnerText.Trim().ToUpper();  // ej: JG, 2X, 3X

				var order = matchedLottery.Order;
				var description = lotteries.FirstOrDefault(l => l.Order == order)?.Name ?? "";
				var isBusted = Constants.BustedList.Contains(plus1Text);

				var awardLine = CreateAwardLine(order, description, numberText, isBusted, papers);
				if(awardLine != null)
					awardLines.Add(awardLine);
			}

			return awardLines;
		}
	}
}