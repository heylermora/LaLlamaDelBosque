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
        public double Receivable { get; set; }
        public double Received { get; set; }
        public List<SummaryClient> MajorDebts { get; set; } = new List<SummaryClient>();
        public List<SummaryClient> MinorDebts { get; set; } = new List<SummaryClient>();
        public List<SummaryClient> InactiveClients { get; set; } = new List<SummaryClient>();
    }

    public class SummaryClient
    {
        public string Name { get; set; } = "";
        public double Amount { get; set; }
    }

    public class SummaryLotteryModel
    {
        public int Papers { get; set; }
        public double TotalAmount { get; set; }
        public double TotalBusted { get; set; }
        public double Total { get; set; }
    }
}
