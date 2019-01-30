using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading;
using log4net;
using Negri.Wot.Mail;
using Negri.Wot.Sql;

namespace Negri.Wot
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        private static int Main(string[] args)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                ParseParans(args, out int ageHours, out int maxClans, out int maxRunMinutes, out bool noSaveOnDatabase,
                    out TimeSpan webFetchInterval, out int maxToAutoAdd, out int hourToAutoAdd,
                    out int autoMinNumberOfMembers);
                var saveOnDatabase = !noSaveOnDatabase;

                var cacheDirectory = ConfigurationManager.AppSettings["CacheDirectory"];

                Log.Info("------------------------------------------------------------------------------------");
                Log.Info("FetchClanMembership iniciando...");
                Log.InfoFormat(
                    "ageHours: {0}; maxClans: {1}; maxRunMinutes: {2}; cacheDirectory: {3}; noSaveOnDatabase:{4}; webFetchInterval:{5}",
                    ageHours, maxClans, maxRunMinutes, cacheDirectory, noSaveOnDatabase, webFetchInterval);

                var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
                var provider = new DbProvider(connectionString);
                var fetcher = new Fetcher(cacheDirectory)
                {
                    WebCacheAge = TimeSpan.FromHours(ageHours - 1),
                    WebFetchInterval = webFetchInterval,
                    ApplicationId = ConfigurationManager.AppSettings["WgApi"]
                };
                var recorder = new DbRecorder(connectionString);

                var mailSender = new MailSender(ConfigurationManager.AppSettings["SmtpHost"],
                    int.Parse(ConfigurationManager.AppSettings["SmtpPort"]),
                    true, ConfigurationManager.AppSettings["SmtpUsername"],
                    ConfigurationManager.AppSettings["SmtpPassword"])
                {
                    From = new MailAddress(ConfigurationManager.AppSettings["SmtpUsername"], "Membership Service")
                };

                // Verifica a necessidade de adicionar novos clãs ao sistema
                var clansToAdd = provider.GetClansToAdd().ToArray();
                foreach (var clanToAdd in clansToAdd)
                {
                    var clanWithId = fetcher.FindClan(clanToAdd.Plataform, clanToAdd.ClanTag);
                    if (clanWithId != null)
                    {
                        var existingClan = provider.GetClan(clanWithId.Plataform, clanWithId.ClanId);
                        if (existingClan != null)
                        {
                            if (existingClan.Enabled)
                            {
                                Log.WarnFormat(
                                    "Foi requerido adicionar o clã {0}@{1}, mas já existe no sistema o clã {2}.{3}@{4}.",
                                    clanToAdd.ClanTag, clanToAdd.Plataform, existingClan.ClanId, existingClan.ClanTag,
                                    existingClan.Plataform);
                                mailSender.Send($"Clã {clanWithId.ClanTag} já existe e está habilitado",
                                    $"Id: {clanWithId.ClanId}; Plataforma: {clanWithId.Plataform}; URL: {GetClanUrl(clanWithId.Plataform, clanWithId.ClanTag)}");
                            }
                            else
                            {
                                // Reabilitar o cla
                                recorder.EnableClan(clanWithId.Plataform, clanWithId.ClanId);
                                mailSender.Send($"Habilitado manualmente o clã {clanWithId.ClanTag}",
                                    $"Id: {clanWithId.ClanId}; Plataforma: {clanWithId.Plataform}; URL: {GetClanUrl(clanWithId.Plataform, clanWithId.ClanTag)}");
                            }
                        }
                        else
                        {
                            clanWithId.Country = clanToAdd.Country;
                            recorder.Add(clanWithId);
                            mailSender.Send($"Adicionado manualmente o clã {clanWithId.ClanTag}",
                                $"Id: {clanWithId.ClanId}; Plataforma: {clanWithId.Plataform}; URL: {GetClanUrl(clanWithId.Plataform, clanWithId.ClanTag)}");
                        }
                    }
                    else
                    {
                        Log.WarnFormat(
                            "Foi requerido adicionar o clã {0}@{1}, mas ele não foi encontrado, ou é muito pequeno.",
                            clanToAdd.ClanTag, clanToAdd.Plataform);
                        mailSender.Send($"Clã {clanToAdd.ClanTag}@{clanToAdd.Plataform} não adicionado",
                            $"Foi requerido adicionar o clã {clanToAdd.ClanTag}@{clanToAdd.Plataform}, mas ele não foi encontrado, ou é muito pequeno.");
                    }
                }

                recorder.ClearClansToAddQueue();

                // Autocadastro de clãs
                if (maxToAutoAdd > 0 && DateTime.UtcNow.Hour == hourToAutoAdd)
                {
                    var toAdd = fetcher.GetClans(Platform.XBOX, autoMinNumberOfMembers)
                        .Where(c => !provider.ClanExists(c.Plataform, c.ClanId))
                        .Concat(fetcher.GetClans(Platform.PS, autoMinNumberOfMembers)
                        .Where(c => !provider.ClanExists(c.Plataform, c.ClanId)))
                        .OrderByDescending(c => c.AllMembersCount).ThenBy(c => c.ClanId).ToArray();

                    foreach (var c in toAdd.Take(maxToAutoAdd))
                    {
                        recorder.Add(c);
                        mailSender.Send($"Adicionado automaticamente o clã {c.ClanTag}@{c.Plataform}",
                            $"Id: {c.ClanId}, Plataforma: {c.Plataform}; Membros: {c.AllMembersCount}; URL: {GetClanUrl(c.Plataform, c.ClanTag)}");
                    }

                    // Readição de clãs pequenos ou que ficaram inativos
                    recorder.ReAddClans(maxToAutoAdd);                    
                }

                var clans = provider.GetClanMembershipUpdateOrder(maxClans, ageHours).ToArray();
                Log.InfoFormat("{0} clãs devem ser atualizados.", clans.Length);

                if (clans.Length == 0)
                {
                    Log.Info("Nenhum clã para atualizar.");
                    return 0;
                }

                var clansToRename = new List<Clan>();
                var clansToUpdate = fetcher.GetClans(clans).ToArray();

                // Clãs debandados
                foreach (var clan in clansToUpdate.Where(c => c.IsDisbanded))
                {
                    var disbandedClan = provider.GetClan(clan.Plataform, clan.ClanId);
                    if (saveOnDatabase)
                    {
                        recorder.DisableClan(disbandedClan.Plataform, disbandedClan.ClanId, DisabledReason.Disbanded);
                    }

                    var sb = new StringBuilder();
                    sb.AppendLine(
                        $"Clã {disbandedClan.ClanTag}, id {disbandedClan.ClanId}, no {disbandedClan.Plataform}, com {disbandedClan.Count} jogadores, foi debandado.");
                    sb.AppendLine($"WN8t15: {disbandedClan.Top15Wn8:N0} em {disbandedClan.Moment:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine($"URL: {GetClanUrl(disbandedClan.Plataform, disbandedClan.ClanTag)}");
                    sb.AppendLine();
                    sb.AppendLine("Jogadores:");
                    foreach (var player in disbandedClan.Players)
                    {
                        sb.AppendLine(
                            $"{player.Name}: WN8 recente de {player.MonthWn8:N0} em {player.MonthBattles:N0}");
                    }

                    mailSender.Send($"Clã {disbandedClan.ClanTag}@{disbandedClan.Plataform} foi debandado",
                        sb.ToString());
                }

                // Clãs que ficaram pequenos demais
                foreach (var clan in clansToUpdate.Where(c => !c.IsDisbanded && c.Count < 4))
                {
                    if (saveOnDatabase)
                    {
                        recorder.DisableClan(clan.Plataform, clan.ClanId, DisabledReason.TooSmall);
                    }

                    mailSender.Send($"Clã {clan.ClanTag}@{clan.Plataform} foi suspenso",
                        $"Clã {clan.ClanTag}@{clan.Plataform} com {clan.Count} jogadores foi suspenso de atualizações futuras. " +
                        $"URL: {GetClanUrl(clan.Plataform, clan.ClanTag)}");
                }

                foreach (var clan in clansToUpdate.Where(c => !c.IsDisbanded && c.Count >= 4))
                {
                    Log.DebugFormat("Clã {0}.{1}@{2}...", clan.ClanId, clan.ClanTag, clan.Plataform);
                    if (saveOnDatabase)
                    {
                        recorder.Set(clan, true);
                        if (clan.HasChangedTag)
                        {
                            clansToRename.Add(clan);
                        }
                    }
                }

                if (clansToRename.Any(c => c.Plataform == Platform.XBOX))
                {
                    var putter = new FtpPutter(ConfigurationManager.AppSettings["FtpFolder"],
                        ConfigurationManager.AppSettings["FtpUser"],
                        ConfigurationManager.AppSettings["FtpPassworld"]);

                    var resultDirectory = Path.Combine(ConfigurationManager.AppSettings["ResultDirectory"], "Clans");

                    foreach (var clan in clansToRename.Where(c => c.Plataform == Platform.XBOX))
                    {
                        Log.InfoFormat("O clã {0}.{1}@{2} teve o tag trocado a partir de {3}.", clan.ClanId,
                            clan.ClanTag, clan.Plataform, clan.OldTag);

                        // Faz copia do arquivo local, e o upload com o novo nome
                        string oldFile = Path.Combine(resultDirectory, $"clan.{clan.OldTag}.json");
                        if (File.Exists(oldFile))
                        {
                            string newFile = Path.Combine(resultDirectory, $"clan.{clan.ClanTag}.json");
                            File.Copy(oldFile, newFile, true);
                            putter.PutClan(newFile);
                            putter.DeleteFile($"Clans/clan.{clan.OldTag}.json");                            
                            putter.SetRenameFile(clan.OldTag, clan.ClanTag);
                        }
                                                
                        mailSender.Send($"Clã Renomeado: {clan.OldTag} -> {clan.ClanTag} em {clan.Plataform}",
                            $"URL: {GetClanUrl(clan.Plataform, clan.ClanTag)}");
                    }
                }

                if (clansToRename.Any(c => c.Plataform == Platform.PS))
                {
                    var putter = new FtpPutter(ConfigurationManager.AppSettings["PsFtpFolder"],
                        ConfigurationManager.AppSettings["PsFtpUser"],
                        ConfigurationManager.AppSettings["PsFtpPassworld"]);

                    var resultDirectory = ConfigurationManager.AppSettings["PsResultDirectory"];

                    foreach (var clan in clansToRename.Where(c => c.Plataform == Platform.XBOX))
                    {
                        Log.InfoFormat("O clã {0}.{1}@{2} teve o tag trocado a partir de {3}.", clan.ClanId,
                            clan.ClanTag, clan.Plataform, clan.OldTag);

                        // Faz copia do arquivo local, e o upload com o novo nome
                        string oldFile = Path.Combine(resultDirectory, $"clan.{clan.OldTag}.json");
                        if (File.Exists(oldFile))
                        {
                            string newFile = Path.Combine(resultDirectory, $"clan.{clan.ClanTag}.json");
                            File.Copy(oldFile, newFile, true);
                            putter.PutClan(newFile);
                            putter.DeleteFile($"Clans/clan.{clan.OldTag}.json");
                            putter.SetRenameFile(clan.OldTag, clan.ClanTag);
                        }

                        mailSender.Send($"Clã Renomeado: {clan.OldTag} -> {clan.ClanTag} em {clan.Plataform}",
                            $"URL: {GetClanUrl(clan.Plataform, clan.ClanTag)}");
                    }
                }

                // Tempo extra para os e-mails terminarem de serem enviados
                Log.Info("Esperando 2 minutos antes de encerrar para terminar de enviar os e-mails...");
                Thread.Sleep(TimeSpan.FromMinutes(2));

                Log.InfoFormat("FetchClanMembership terminando normalmente em {0}.", sw.Elapsed);
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex);
                Console.WriteLine(ex);
                return 1;
            }
        }

        private static string GetClanUrl(Platform platform, string clanTag)
        {
            return
                $"https://{(platform == Platform.PS ? "ps." : "")}wotclans.com.br/Clan/{clanTag.ToUpperInvariant()}";
        }

        private static void ParseParans(string[] args, out int ageHours, out int maxClans, out int maxRunMinutes,
            out bool noSaveOnDatabase, out TimeSpan webFetchInterval, out int maxToAutoAdd, out int hourToAutoAdd,
            out int autoMinNumberOfMembers)
        {
            ageHours = int.Parse(args[0]);
            maxClans = int.Parse(args[1]);
            maxRunMinutes = int.Parse(args[2]);

            webFetchInterval = TimeSpan.FromSeconds(0);
            noSaveOnDatabase = false;
            hourToAutoAdd = 17; // UTC
            autoMinNumberOfMembers = 15;
            maxToAutoAdd = 10;
            for (var i = 3; i < args.Length; ++i)
            {
                var arg = args[i];

                if (arg.StartsWith("WebFetchIntervalSeconds:"))
                {
                    webFetchInterval = TimeSpan.FromSeconds(int.Parse(arg.Substring(24)));
                }
                else if (arg == "NoSaveOnDatabase")
                {
                    noSaveOnDatabase = true;
                }
                else if (arg.Contains("MaxToAutoAdd:"))
                {
                    maxToAutoAdd = int.Parse(arg.Substring("MaxToAutoAdd:".Length));
                }
                else if (arg.Contains("HourToAutoAdd:"))
                {
                    hourToAutoAdd = int.Parse(arg.Substring("HourToAutoAdd:".Length));
                }
                else if (arg.Contains("AutoMinNumberOfMembers:"))
                {
                    autoMinNumberOfMembers = int.Parse(arg.Substring("AutoMinNumberOfMembers:".Length));
                }
            }
        }
    }
}