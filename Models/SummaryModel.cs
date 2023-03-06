using System.ComponentModel.DataAnnotations;

namespace LaLlamaDelBosque.Models
{
    public class SummaryModel
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
}
