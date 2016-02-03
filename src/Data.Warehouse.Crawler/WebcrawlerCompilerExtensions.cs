namespace Data.Warehouse.Crawler
{
    public static class WebcrawlerCompilerExtensions
    {
        public static int GetLevel(this string line)
        {
            var level = default(int);
            for (int c = 0; c <= line.Length - 1; c++) { if (line[c] != ' ') { break; } level++; }
            return level;
        }
    }
}
