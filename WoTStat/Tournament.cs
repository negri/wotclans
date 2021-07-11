using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Negri.Wot
{
    /// <summary>
    /// Um torneio
    /// </summary>
    public class Tournament
    {
        /// <summary>
        /// Nome super curto, para ser a URL do torneio
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// Nome (curto) do torneio, para aparecer no menu
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Descrição do Torneio (aparece no box em letras enormes)
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// A versão do campeonato, por exemplo 'Summer 2017', 'Season 2' etc
        /// </summary>
        public string Instalment { get; set; }

        /// <summary>
        /// Se restrito não aparece no menu de torneios 
        /// </summary>
        public bool IsRestricted { get; set; }

        /// <summary>
        /// Quando começa o torneio
        /// </summary>
        public DateTime Start { get; set; }

        /// <summary>
        /// Quando termina o torneio
        /// </summary>
        public DateTime End { get; set; }

        /// <summary>
        /// URL com as chaves
        /// </summary>
        public string BracketsUrl { get; set; }

        /// <summary>
        /// URL para mais informações sobre o campeonato
        /// </summary>
        public string InformationUrl { get; set; }

        /// <summary>
        /// URL para video sobre o evento
        /// </summary>
        public string VideoUrl { get; set; }

        /// <summary>
        /// URL para forum do evento
        /// </summary>
        public string ForumUrl { get; set; }

        /// <summary>
        /// URL para o Discord do evento
        /// </summary>
        public string DiscordUrl { get; set; }

        /// <summary>
        /// URL para as regras do Evento
        /// </summary>
        public string RulesUrl { get; set; }

        /// <summary>
        /// Array das linguas para que esse campeonato deve aparecer. Se vazio aparece em todas
        /// </summary>
        public string[] ExclusiveLanguages { get; set; } = new string[0];
        
        /// <summary>
        /// Os clãs que fazem parte do torneio
        /// </summary>
        public string[] Clans { get; set; } = new string[0];

        /// <summary>
        /// Se esse torneio deve ou não aparecer no menu para quem tem uma certa linguagem
        /// </summary>
        public bool ShouldAppear(string language)
        {
            if ((ExclusiveLanguages == null) || (ExclusiveLanguages.Length == 0))
            {
                return true;
            }

            return ExclusiveLanguages.Any(x => string.Equals(x, language, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Se esse torneio deve ou não aparecer no menu, com relação a data em que está sendo listado
        /// </summary>
        public bool ShouldAppear(DateTime date)
        {
            return (Start.AddDays(-14) <= date) && (date <= End.AddDays(14));
        }

        public static IEnumerable<Tournament> ReadAll(bool includeRestricted = false)
        {
            var dataDirectory = ConfigurationManager.AppSettings["ClanResultsFolder"];                        
            var currentLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var date = DateTime.UtcNow;

            var all = ReadAll(dataDirectory).Where(t => t.ShouldAppear(currentLanguage) && t.ShouldAppear(date)).OrderBy(t => t.Name);
            return includeRestricted ? all : all.Where(t => !t.IsRestricted);
        }

        /// <summary>
        /// Le todos os torneios de um diretorio
        /// </summary>
        public static IEnumerable<Tournament> ReadAll(string baseDirectory)
        {
            var tournamentDirectory = Path.Combine(baseDirectory, "Tournament");
            var di = new DirectoryInfo(tournamentDirectory);
            return from fi in di.EnumerateFiles("*.json", SearchOption.TopDirectoryOnly) where fi.Length > 0 select Read(fi.FullName);
        }

        public static Tournament Read(string baseDirectory, string tournament)
        {
            var tournamentDirectory = Path.Combine(baseDirectory, "Tournament");
            return Read(Path.Combine(tournamentDirectory, $"{tournament}.json"));
        }

        /// <summary>
        /// Lê um torneio de um arquivo
        /// </summary>
        public static Tournament Read(string fileName)
        {
            var json = File.ReadAllText(fileName, Encoding.UTF8);
            return JsonConvert.DeserializeObject<Tournament>(json);
        }

        /// <summary>
        /// Salva o torneio no diretorio
        /// </summary>
        /// <param name="baseDirectory">The data directory.</param>
        public void Save(string baseDirectory)
        {
            var tournamentDirectory = Path.Combine(baseDirectory, "Tournament");
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(Path.Combine(tournamentDirectory, $"{Tag}.json"), json, Encoding.UTF8);
        }
    }
}