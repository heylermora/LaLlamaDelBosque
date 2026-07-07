namespace LaLlamaDelBosque.Models
{
    public class AwardConnectionModel
    {
        public int AwardId { get; set; }
        public int LineId { get; set; }
        public AwardLine AwardLine { get; set; } = new AwardLine();
        public IList<Lottery> AvailableLotteries { get; set; } = new List<Lottery>();
    }
}
