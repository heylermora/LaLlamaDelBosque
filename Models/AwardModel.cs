namespace LaLlamaDelBosque.Models
{
    public class AwardModel
    {
        public IList<Award> Awards { get; set; } = new List<Award>();
    }
    public class AwardLine
    {
        public int Order { get; set; }
        public string Description { get; set; } = "";
        public string Number { get; set; } = "";
        public double Busted { get; set; }
        public double Amount { get; set; }
        public double TimesBusted { get; set; } = 200;
        public double TimesAmount { get; set; } = 85;
        public double Award { get; set; }
        public bool IsBusted { get; set; }
    }
    public class Award
    {
        public int Id { get; set; }
        public DateTime Date { get; set; } = new DateTime();
        public IList<AwardLine> AwardLines { get; set; } = new List<AwardLine>();

    }
}
