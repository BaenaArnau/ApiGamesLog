using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApiGamesLog
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            await MetacriticScraper.ScrapeAndSaveAllPagesAsync("https://www.metacritic.com/browse/game/?releaseYearMin=1958&releaseYearMax=2024", 563);

        }
    }
}
