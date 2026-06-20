using LaLlamaDelBosque.Interfaces;
using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Services.Scrapers;
using LaLlamaDelBosque.Utils;
using NuGet.Packaging;

namespace LaLlamaDelBosque.Services
{
    public class ScrapingService
    {
        private readonly List<ScrapingLottery> _scrapingLotteries;
        private readonly List<Lottery> _lotteries;
        private readonly List<Paper> _papers;
        private readonly HttpClient _httpClient;

        public ScrapingService()
        {
            _scrapingLotteries = GetScrapingLotteries();
            _lotteries = GetLotteries();
            _papers = GetPapers();

            _httpClient = new HttpClient();
        }

        public async Task<Award> Add()
        {
            var award = new Award()
            {
                Date = DateTime.Today,
                AwardLines = new List<AwardLine>()
            };

            var scrapers = new List<IScraperStrategy>
            {
                new JpsNuevosTiemposScraper(_httpClient),
                new NicaraguaLotoDiariaScraper(_httpClient),
                new DominicanaLaPrimeraScraper(_httpClient),
                new HondurasLotoDiariaScraper(_httpClient)
            };

            foreach(var scraper in scrapers)
            {
                var awardLines = await scraper.ScrapeAwards(_scrapingLotteries, _lotteries, _papers);
                award.AwardLines.AddRange(awardLines);
            }

            AddMissingExpectedAwardLines(award);
            award.AwardLines = award.AwardLines.OrderBy(x => x.Order).ToList();
            return award;
        }

        private void AddMissingExpectedAwardLines(Award award)
        {
            var now = DateTime.Now.TimeOfDay;
            var todayName = DateTime.Today.DayOfWeek.ToString();
            var foundOrders = award.AwardLines.Select(x => x.Order).ToHashSet();

            var missingLines = _lotteries
                .Where(lottery => lottery.Hour <= now)
                .Where(lottery => lottery.Days == null || lottery.Days.Count == 0 || lottery.Days.Contains(todayName))
                .Where(lottery => !foundOrders.Contains(lottery.Order))
                .Select(lottery => new AwardLine
                {
                    Order = lottery.Order,
                    Description = lottery.Name,
                    Number = "No encontrado",
                    MissingReason = $"No se encontró resultado en la fuente para el sorteo programado a las {DateTime.Today.Add(lottery.Hour):h:mm tt}. Puede que la fuente no haya publicado el resultado, haya cambiado el formato o el scraper no haya hecho match."
                });

            award.AwardLines.AddRange(missingLines);
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
