using System.Collections.Generic;

namespace Data.Warehouse.Crawler
{
    public class WebcrawlerCommand
    {
        public string AttributID { get; set; }
        public string Command { get; set; }
        public string Target { get; set; }
        public bool IsLoop { get; set; }
        public IEnumerable<WebcrawlerCommand> Subcommands { get; set; }
    }
}
