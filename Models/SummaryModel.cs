namespace LaLlamaDelBosque.Models
{

    public class SummaryModel
    {
        public SummaryCreditModel Credit { get; set; } = new SummaryCreditModel();
        public SummaryLotteryModel Lottery { get; set; } = new SummaryLotteryModel();

    }

    public class SummaryCreditModel
    {
        public int Clients { get; set; }
        public string ClientsDescription { get; set; } = "personas";
        public string Receivable { get; set; } = "";
        public string Received { get; set; } = "";
        public List<SummaryClient> MajorDebts { get; set; } = new List<SummaryClient>();
        public List<SummaryClient> MinorDebts { get; set; } = new List<SummaryClient>();
        public List<SummaryClient> InactiveClients { get; set; } = new List<SummaryClient>();
    }

    public class SummaryClient
    {
        public string Name { get; set; } = "";
        public string Amount { get; set; } = "";
    }

    public class SummaryLotteryModel
    {
        public int Papers { get; set; }
        public string TotalAmount { get; set; } = string.Empty;
        public string TotalBusted { get; set; } = string.Empty;
        public string Total { get; set; } = string.Empty;

    }

}
