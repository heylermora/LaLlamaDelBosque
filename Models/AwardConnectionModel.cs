namespace LaLlamaDelBosque.Models
{
    public class AwardConnectionModel
    {
        public int AwardId { get; set; }
        public int LineId { get; set; }
        public AwardLine AwardLine { get; set; } = new AwardLine();
    }
}
