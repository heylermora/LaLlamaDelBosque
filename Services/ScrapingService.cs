using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using System.Web.Mvc;

namespace LaLlamaDelBosque.Services
{
	public class ScrapingService
	{
		private readonly List<ScrapingLottery> _scrapinglotteries;
		private readonly List<Lottery> _lotteries;
		private readonly List<Paper> _papers;

		public ScrapingService()
		{
			_scrapinglotteries = GetScrapingLotteries();
			_lotteries = GetLotteries();
			_papers = GetPapers();
		}

		public Award Add(List<string>? descriptions)
		{
			var award = new Award()
			{
				Date = DateTime.Today,
			};
			string url = "https://www.loteriasloterias.com/";
			var web = new HtmlWeb();
			var doc = web.Load(url);


			foreach(var scrapinglottery in _scrapinglotteries)
			{
				var index = 0;
				var line = new AwardLine();
				var tableNode = doc.DocumentNode.SelectSingleNode($"//table[@class='premios']//tr[td='{scrapinglottery.Name}'][td='{scrapinglottery.Hour}']");
					
				if(tableNode != null)
				{
					var description = _lotteries.First(x => x.Order == scrapinglottery.Order).Name;
					if(!descriptions?.Contains(description) ?? true)
					{
						foreach(var tdNode in tableNode.Descendants("td"))
						{
							var value = tdNode.InnerText;
							switch(index)
							{
								case 0:

									line.Order = scrapinglottery.Order;
									line.Description = description;
									break;
								case 2:
									var papers = _papers.Where(x => x.Lottery == description && x.DrawDate.ToShortDateString() == DateTime.Today.ToShortDateString() && x.Numbers.Any(x => x.Value == value));
									var amount = papers.Sum(x => x.Numbers.Sum(n => n.Value == value ? n.Amount : 0));
									var busted = papers.Sum(x => x.Numbers.Sum(n => n.Value == value ? n.Busted : 0));
									line.Number = value;
									line.Amount = amount;
									line.Busted = busted;
									line.Award = line.TimesAmount * amount + line.TimesBusted * busted;
									break;
							}
							index++;
						}
						award.AwardLines.Add(line);
					}
				}

			}

			return award;
		}

		private static List<ScrapingLottery> GetScrapingLotteries()
		{
			var lotteries = JsonFile.Read("ScrapingLotteries", new ScrapingLotteryModel());
			return lotteries.Lotteries;
		}

		private static List<Lottery> GetLotteries()
		{
			var lotteries = JsonFile.Read("Lotteries", new LotteryModel());
			return lotteries.Lotteries;
		}

		private static List<Paper> GetPapers()
		{
			var papers = JsonFile.Read("Papers", new PaperModel());
			return papers.Papers;
		}
	}
}
