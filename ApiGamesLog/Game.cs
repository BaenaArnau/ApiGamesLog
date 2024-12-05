using System;

namespace ApiGamesLog
{
    public class Game
    {
        public int GameId { get; set; } 
        public string Title { get; set; } 
        public string GameDescription { get; set; } 
        public int? MetacriticScore { get; set; } 
        public byte[] CoverImage { get; set; } 
        public DateTime? ReleaseDate { get; set; }
    }
}