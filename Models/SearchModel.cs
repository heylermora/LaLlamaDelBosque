namespace LaLlamaDelBosque.Models
{
    public class SearchModel
    {
        public string Id { get; set; } = "";

        public string SearchText { get; set; } = "";

        public IEnumerable<Note> SearchResult { get; set; } = new List<Note>();
    }
}
