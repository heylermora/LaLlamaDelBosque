﻿using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Services.Scrapers;
using LaLlamaDelBosque.Utils;
using System.Text.RegularExpressions;

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
							if(!DateTime.TryParse(cells[0]?.InnerText.Trim(), out var extractedDate) || extractedDate.Date != DateTime.Today)
							{
								continue;
							}

							var order = scrapingLotteries.First(x => x.Name == cells[1]?.InnerText.Trim()).Order;
							var description = lotteries.First(x => x.Order == order).Name;

							var values = cells[2]?.InnerText.Trim().Split(" ");
							if(!string.IsNullOrEmpty(values?[0]) && !string.IsNullOrEmpty(values?[1]))
							{
								papers = papers.Where(x => x.Lottery == description && x.DrawDate.ToShortDateString() == DateTime.Today.ToShortDateString() && x.Numbers.Any(x => x.Value == values[0])).ToList();
								var amount = papers.Sum(x => x.Numbers.Sum(n => n.Value == values[0] ? n.Amount : 0));
								var busted = papers.Sum(x => x.Numbers.Sum(n => n.Value == values[0] ? n.Busted : 0));

								var isBusted = Constants.BustedList.Contains(Regex.Replace(values[1], @"\(([^)]+)\)", "$1"));
								var awardLine = CreateAwardLine(order, description, values[0].Trim(), isBusted, papers);
								if(awardLine != null)
								{
									awardLines.Add(awardLine);
								}
							}
						}
					}
				}
			}

			return awardLines;
		}
	}
}
