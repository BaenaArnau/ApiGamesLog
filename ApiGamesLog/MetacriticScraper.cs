using ApiGamesLog;
using HtmlAgilityPack;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using Dapper;

public class MetacriticScraper
{
    private static readonly HttpClient client = new HttpClient();
    private const string ConnectionString = "Data Source=85.208.21.117,54321;" +
                "Initial Catalog=BBDDGamesLog;" +
                "User ID=sa;" +
                "Password=Sql#123456789;" +
                "TrustServerCertificate=True;"; // Reemplaza con tu cadena de conexión a la base de datos

    public static async Task ScrapeAndSaveAllPagesAsync(string baseUrl, int totalPages)
    {
        int currentPage = 1;
        int gamesOnPage = 0;

        while (currentPage <= totalPages)  // Empezamos desde la página 1
        {
            var url = $"{baseUrl}&page={currentPage}"; // Reemplazamos el número de página en la URL
            Console.WriteLine($"Scraping page {currentPage} of {totalPages} - URL: {url}");
            gamesOnPage = await ScrapeAndSaveGamesAsync(url);

            // Si llegamos a 24 juegos, pasamos a la siguiente página
            if (gamesOnPage == 24)
            {
                currentPage++;
            }
            else
            {
                // Si no se obtienen 24 juegos, hemos llegado al final de las páginas disponibles
                break;
            }
        }
    }

    public static async Task<int> ScrapeAndSaveGamesAsync(string url)
    {
        try
        {
            var response = await client.GetStringAsync(url);

            if (string.IsNullOrEmpty(response))
            {
                // Si no se pudo obtener el contenido, devolver 0 y no guardar ningún juego
                Console.WriteLine($"No se pudo obtener contenido de la URL: {url}");
                return 0;
            }

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(response);

            var games = new List<Game>();

            var gameNodes = htmlDocument.DocumentNode.SelectNodes("//div[contains(@class, 'c-finderProductCard c-finderProductCard-game')]");

            if (gameNodes == null)
            {
                Console.WriteLine("No se encontraron nodos de juegos en la página.");
                return 0;
            }

            foreach (var gameNode in gameNodes)
            {
                var titleNode = gameNode.SelectSingleNode(".//h3[contains(@class, 'c-finderProductCard_titleHeading')]/span[2]");
                var descriptionNode = gameNode.SelectSingleNode(".//div[contains(@class, 'c-finderProductCard_description')]/span[1]");
                var scoreNode = gameNode.SelectSingleNode(".//div[contains(@class, 'c-siteReviewScore u-flexbox-column u-flexbox-alignCenter u-flexbox-justifyCenter g-text-bold c-siteReviewScore_green g-color-gray90 c-siteReviewScore_xsmall')]/span");
                var releaseDateNode = gameNode.SelectSingleNode(".//span[contains(@class, 'u-text-uppercase')]");

                if (titleNode == null || descriptionNode == null || scoreNode == null || releaseDateNode == null)
                {
                    Console.WriteLine("Faltan datos en un nodo del juego. Continuando con el siguiente.");
                    continue;
                }

                var title = titleNode.InnerText.Trim();
                var gameDescription = descriptionNode.InnerText.Trim();
                var metacriticScore = int.Parse(scoreNode.InnerText.Trim());
                var releaseDate = DateTime.Parse(releaseDateNode.InnerText.Trim());
                var coverImageNode = gameNode.SelectSingleNode(".//div[contains(@class, 'c-finderProductCard_img')]/picture//img");
                var coverImageUrl = coverImageNode?.GetAttributeValue("src", "");

                byte[] coverImageBytes = null;

                if (coverImageUrl != null)
                {
                    // Intentamos descargar la imagen solo si la URL no es null
                    coverImageBytes = await DownloadImageAsync(coverImageUrl);
                    if (coverImageBytes == null)
                    {
                        Console.WriteLine("No se pudo descargar la imagen del juego, se guardará como null.");
                    }
                }

                // Agregar el juego a la lista, incluso si coverImageBytes es null
                games.Add(new Game
                {
                    Title = title,
                    GameDescription = gameDescription,
                    MetacriticScore = metacriticScore,
                    CoverImage = coverImageBytes,  // Guardamos null si no se descargó la imagen
                    ReleaseDate = releaseDate,
                });
            }

            // Guardar los juegos en la base de datos después de procesar todos
            SaveGamesToDatabase(games);

            return games.Count;  // Devuelve el número de juegos encontrados en esta página
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al procesar la URL: {url} - {ex.Message}");
            return 0;
        }
    }


    private static async Task<byte[]> DownloadImageAsync(string imageUrl)
    {
        try
        {
            // Verifica si la URL es relativa (comienza con '/')
            if (!Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute))
            {
                // Concatenar la URL base con la URL relativa
                var baseUrl = "https://www.metacritic.com";  // URL base adecuada
                imageUrl = baseUrl + imageUrl;
            }

            var requestMessage = new HttpRequestMessage(HttpMethod.Get, imageUrl);
            requestMessage.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

            var response = await client.SendAsync(requestMessage);

            // Verificar si la respuesta es exitosa
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync();
            }
            else
            {
                // Si no se pudo descargar la imagen, se retorna null
                Console.WriteLine($"Error al descargar la imagen. Estado HTTP: {response.StatusCode}");
                return null;
            }
        }
        catch (Exception ex)
        {
            // Loguear el error y devolver null, sin interrumpir la ejecución
            Console.WriteLine($"Error al descargar la imagen: {ex.Message}");
            return null;
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
