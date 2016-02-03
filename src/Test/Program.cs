using Data.Mining;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var filter = MiningCompiler.ComposeFilter("Neue, abendrote Kleidung in den Größen S bis M bis maximal 60,50 EUR");
        }
    }
}
