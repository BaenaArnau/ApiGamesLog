using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using System.Net.Http;

namespace ApiGamesLog
{
    public class MetacriticScraper
    {
        private const string ConnectionString = "Data Source=85.208.21.117,54321;" +
                    "Initial Catalog=BBDDGamesLog;" +
                    "User ID=sa;" +
                    "Password=Sql#123456789;" +
                    "TrustServerCertificate=True;"; // Reemplaza con tu cadena de conexión a la base de datos

        public static async Task ScrapeAndSaveAllPagesAsync(string baseUrl, int totalPages)
        {
            var tasks = new List<Task>();

            // Utilizamos Parallel.ForEach para procesar múltiples páginas al mismo tiempo
            Parallel.For(1, totalPages + 1, (currentPage) =>
            {
                tasks.Add(Task.Run(async () =>
                {
                    var url = $"{baseUrl}&page={currentPage}";
                    Console.WriteLine($"Scraping page {currentPage} of {totalPages} - URL: {url}");
                    await ScrapeAndSaveGamesAsync(url);
                }));
            });

            await Task.WhenAll(tasks);
        }

        public static async Task ScrapeAndSaveGamesAsync(string url)
        {
            try
            {
                var options = new ChromeOptions();
                options.AddArgument("--headless"); // Ejecutar Chrome en modo headless
                options.AddArgument("--disable-gpu"); // Necesario para algunos entornos de ejecución
                options.AddArgument("--no-sandbox");

                using (var driver = new ChromeDriver(options))
                {
                    driver.Navigate().GoToUrl(url);
                    // Esperar a que se cargue la página
                    await Task.Delay(5000); // Espera 5 segundos para que se carguen los elementos dinámicos

                    var games = new List<Game>();

                    var gameNodes = driver.FindElements(By.XPath("//div[contains(@class, 'c-finderProductCard c-finderProductCard-game')]"));

                    if (gameNodes.Count == 0)
                    {
                        Console.WriteLine("No se encontraron nodos de juegos en la página.");
                        return;
                    }

                    foreach (var gameNode in gameNodes)
                    {
                        var titleNode = gameNode.FindElement(By.XPath(".//h3[contains(@class, 'c-finderProductCard_titleHeading')]/span[2]"));
                        var descriptionNode = gameNode.FindElement(By.XPath(".//div[contains(@class, 'c-finderProductCard_description')]/span[1]"));
                        var scoreNode = gameNode.FindElement(By.XPath(".//div[contains(@class, 'c-siteReviewScore u-flexbox-column u-flexbox-alignCenter u-flexbox-justifyCenter g-text-bold c-siteReviewScore_green g-color-gray90 c-siteReviewScore_xsmall')]/span"));
                        var releaseDateNode = gameNode.FindElement(By.XPath(".//span[contains(@class, 'u-text-uppercase')]"));
                        var coverImageNode = gameNode.FindElement(By.XPath(".//div[contains(@class, 'c-finderProductCard_img')]/picture//img"));

                        if (titleNode == null || descriptionNode == null || scoreNode == null || releaseDateNode == null || coverImageNode == null)
                        {
                            Console.WriteLine("Faltan datos en un nodo del juego. Continuando con el siguiente.");
                            continue;
                        }

                        var title = titleNode.Text.Trim();
                        var gameDescription = descriptionNode.Text.Trim();
                        var metacriticScore = int.Parse(scoreNode.Text.Trim());
                        var releaseDate = DateTime.Parse(releaseDateNode.Text.Trim());
                        var coverImageUrl = coverImageNode.GetAttribute("src");

                        if (string.IsNullOrEmpty(coverImageUrl))
                        {
                            Console.WriteLine("No se pudo obtener la URL de la imagen del juego, no se guardará el juego.");
                            continue;
                        }

                        byte[] coverImageBytes = await DownloadImageAsync(coverImageUrl);
                        if (coverImageBytes == null)
                        {
                            Console.WriteLine("No se pudo descargar la imagen del juego, no se guardará el juego.");
                            continue;
                        }

                        games.Add(new Game
                        {
                            Title = title,
                            GameDescription = gameDescription,
                            MetacriticScore = metacriticScore,
                            CoverImage = coverImageBytes,
                            ReleaseDate = releaseDate,
                        });
                    }

                    // Guardar los juegos en la base de datos después de procesar todos
                    SaveGamesToDatabase(games);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al procesar la URL: {url} - {ex.Message}");
            }
        }

        private static async Task<byte[]> DownloadImageAsync(string imageUrl)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync(imageUrl);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsByteArrayAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al descargar la imagen: {ex.Message}");
                    return null;
                }
            }
        }

        private static void SaveGamesToDatabase(List<Game> games)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                foreach (var game in games)
                {
                    connection.Execute(
                        "INSERT INTO Game (Title, GameDescription, MetacriticScore, CoverImage, ReleaseDate) VALUES (@Title, @GameDescription, @MetacriticScore, @CoverImage, @ReleaseDate)",
                        new
                        {
                            game.Title,
                            game.GameDescription,
                            game.MetacriticScore,
                            game.CoverImage,
                            game.ReleaseDate,
                        });
                }
            }
        }
    }
}