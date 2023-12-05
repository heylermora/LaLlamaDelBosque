namespace LaLlamaDelBosque.Models
{
    public class CreditConnectionModel
    {
        public int ClientId { get; set; }
        public int LineId { get; set; }
        public CreditLine CreditLine { get; set; } = new CreditLine();
    }
}
