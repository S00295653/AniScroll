namespace AniScroll.Models
{
    public class AnimeCard
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string ImageUrl { get; set; }
        public string BannerUrl { get; set; }
        public string Score { get; set; }
        public string Description { get; set; }
        public string Season { get; set; }
        public int? Year { get; set; }
        public string Status { get; set; }
        public string Episodes { get; set; }
        public List<string> Genres { get; set; }

        public AnimeCard()
        {
            Genres = new List<string>();
        }
    }
}