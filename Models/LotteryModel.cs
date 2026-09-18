using System.ComponentModel.DataAnnotations;

namespace LaLlamaDelBosque.Models
{

    public class LotteryModel
    {
        public List<Lottery> Lotteries { get; set; } = new List<Lottery>();
    }

    public class ScrapingConfiguration
    {
        public List<ScrapingProcessConfiguration> Processes { get; set; } = new();
    }

    public class ScrapingProcessConfiguration
    {
        public string Type { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public List<ScrapingDrawConfiguration> Draws { get; set; } = new();
        public List<ScrapingSourceConfiguration> Sources { get; set; } = new();
    }

    public class Lottery
    {
        public int Order { get; set; }
        public string Name { get; set; } = "";
        public TimeSpan Hour { get; set; }
        public bool? Busted { get; set; }
        public List<string>? Days { get; set; }
    }

    public class ScrapingDrawConfiguration
    {
        public int Order { get; set; }
        public string Name { get; set; } = "";
        public string Hour { get; set; } = "";
        public string SourceKey { get; set; } = "";
        public int SortOrder { get; set; }
        public bool Enabled { get; set; } = true;
        public bool Busted { get; set; }
        public List<string> ScrapingNames { get; set; } = new();
        public List<string> ScrapingLotteryNames { get; set; } = new();
        public List<string> ScrapingHours { get; set; } = new();
        public List<string> ScrapingKeys { get; set; } = new();
    }

    public class ScrapingSourceConfiguration
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public string Referrer { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public bool ShowToUser { get; set; } = true;
        public bool IsDedicatedCostaRicaPage { get; set; }
        public int TimeoutSeconds { get; set; } = 15;
    }

    public class Number
    {
        public int? Id { get; set; }
        public string? Value { get; set; }
        public double Amount { get; set; }
        public double Busted { get; set; }
    }

    public class Numbers
    {
        [Required(ErrorMessage = "El campo es requerido.")]
        public string? Value { get; set; } = null;

        [Required(ErrorMessage = "El campo es requerido.")]
        [Range(1, double.MaxValue, ErrorMessage = "El campo debe ser mayor que 0.")]
        public double Amount { get; set; }

        [Required(ErrorMessage = "El campo es requerido.")]
        [Range(0, double.MaxValue, ErrorMessage = "El campo debe ser mayor o igual que 0.")]
        public double? Busted { get; set; } = 0;
    }

    public class PaperModel
    {
        public List<Paper> Papers { get; set; } = new List<Paper>();
    }

    public class Paper
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "El campo es requerido.")]
        public string Lottery { get; set; } = "";
        [Required(ErrorMessage = "El campo es requerido.")]
        public DateTime CreationDate { get; set; }
        [Required(ErrorMessage = "El campo es requerido.")]
        public DateTime DrawDate { get; set; }
		public List<string> SelectedLotteries { get; set; } = new();
		public List<Number> Numbers { get; set; } = new List<Number>();
        public int? ClientId { get; set; }
    }

}
