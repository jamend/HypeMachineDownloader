namespace HypeMachineDownloader.Models
{
    public class TrackModel
    {
        public string type { get; set; }
        public string id { get; set; }
        public int time { get; set; }
        public int ts { get; set; }
        public int postid { get; set; }
        public string posturl { get; set; }
        public int fav { get; set; }
        public string key { get; set; }
        public string artist { get; set; }
        public string song { get; set; }
        public bool is_sc { get; set; }
        public bool is_bc { get; set; }
        public string name { get { return artist + " - " + song; } }
    }
}
