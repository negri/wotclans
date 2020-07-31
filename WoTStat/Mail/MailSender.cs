using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Negri.Wot.Diagnostics;

namespace Negri.Wot.Mail
{
    /// <summary>
    ///     Para enviar e-mails
    /// </summary>
    public class MailSender
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MailSender));

        private readonly string _host;
        private readonly string _password;
        private readonly int _port;
        private readonly string _userName;
        private readonly bool _useSsl;

        public MailSender(string host, int port, bool useSsl, string userName, string password)
        {
            _host = host;
            _port = port;
            _useSsl = useSsl;
            _userName = userName;
            _password = password;

            From = new MailAddress("service@wotclans.com.br", "WoTClans Service");
            To = new MailAddress("admin@wotclans.com.br", "WoTClans Admin");
        }

        /// <summary>
        ///     From padrão
        /// </summary>
        public MailAddress From { get; set; }

        /// <summary>
        ///     To Padrão
        /// </summary>
        public MailAddress To { get; set; }

        /// <summary>
        ///     Manda e-mail de status
        /// </summary>
        /// <param name="siteDiagnostic">Status do Site</param>
        /// <param name="dataDiagnostic">Status do BD</param>
        /// <param name="minPerDay">Mínimo de Players por Dia</param>
        /// <param name="maxPerDay">Máximo de Players por Dia</param>
        /// <param name="calculationTime"></param>
        /// <param name="doneCount"></param>
        /// <param name="timedOut"></param>
        public void SendStatusMessage(SiteDiagnostic siteDiagnostic, DataDiagnostic dataDiagnostic, int minPerDay,
            int maxPerDay, TimeSpan calculationTime, int doneCount, bool timedOut)
        {
            var encoding = Encoding.GetEncoding("iso-8859-1");

            var mailMessage = new MailMessage
            {
                Priority = MailPriority.Low,
                From = From,
                IsBodyHtml = false,
                BodyEncoding = encoding,
                SubjectEncoding = encoding,
                HeadersEncoding = encoding,
                To = {To},
                Body = GetStatusMessageBody(siteDiagnostic, dataDiagnostic, minPerDay, maxPerDay, calculationTime,
                    doneCount, timedOut,
                    out var mailPriority)
            };

            mailMessage.Priority = mailPriority;
            mailMessage.Subject =
                $"WoTClans - Status - {(mailPriority == MailPriority.Low ? "Ok" : mailPriority == MailPriority.Normal ? "Cuidado" : "ALERTA")}";

            Send(mailMessage);
        }

        public void Send(string subject, string body)
        {
            var encoding = Encoding.GetEncoding("iso-8859-1");

            var mailMessage = new MailMessage
            {
                Priority = MailPriority.Low,
                From = From,
                IsBodyHtml = false,
                BodyEncoding = encoding,
                SubjectEncoding = encoding,
                HeadersEncoding = encoding,
                To = {To},
                Subject = subject,
                Body = body
            };

            Send(mailMessage);
        }

        private static string GetStatusMessageBody(SiteDiagnostic sd, DataDiagnostic dd,
            int minPerDay, int maxPerDay, TimeSpan calculationTime, int doneCount, bool timedOut,
            out MailPriority mailPriority)
        {
            // Testes de Priorização
            mailPriority = MailPriority.Low;

            var sb = new StringBuilder();

            sb.AppendLine(
                $"Calculados {doneCount} clãs em {calculationTime}. {calculationTime.TotalSeconds / doneCount:N0}s/clã");
            if (timedOut) sb.AppendLine("Tempo de Execução Esgotado antes do fim!");
            sb.AppendLine();

            sb.AppendLine("No Servidor do Site:");

            sb.AppendFormat("Idade dos dados: {0}min", sd.DataAgeMinutes);
            if (sd.DataAgeMinutes > 30)
            {
                sb.AppendLine(" !");
                mailPriority = Max(mailPriority, MailPriority.Normal);
            }
            else
            {
                sb.AppendLine();
            }

            sb.AppendFormat("Clãs:      {0:N0}", sd.ClansCount);
            sb.AppendLine();
            sb.AppendFormat("Jogadores: {0:N0}", sd.PlayersCount);
            sb.AppendLine();
            sb.AppendFormat("Data MoE:     {0:yyyy-MM-dd}", sd.TanksMoELastDate);
            sb.AppendLine();
            sb.AppendFormat("Data Leaders: {0:yyyy-MM-dd}", sd.TankLeadersLastDate);
            sb.AppendLine();
            sb.AppendFormat("Clãs de jog. at. na últ hora:    {0:0}", sd.ClansWithPlayersUpdatedOnLastHour);
            sb.AppendLine();
            sb.AppendFormat("Clãs com alguma at. na últ hora: {0:0}", sd.ClansWithAnyUpdatedOnLastHour);
            sb.AppendLine();

            sb.AppendFormat("CPU: {0:N2}%; Memória: {1:N0}MB; Threads: {2}",
                sd.AveragedProcessCpuUsage.SinceStartedLoad * 100.0,
                sd.ProcessMemoryUsage.WorkingSetkB / 1024,
                sd.ProcessMemoryUsage.ThreadCount);
            if (sd.AveragedProcessCpuUsage.SinceStartedLoad > 0.20)
            {
                sb.AppendLine(" !!!");
                mailPriority = Max(mailPriority, MailPriority.High);
            }
            else if (sd.ProcessMemoryUsage.WorkingSetMB >= (256 * 3))
            {
                sb.AppendLine(" !!!");
                mailPriority = Max(mailPriority, MailPriority.High);
            }
            else
            {
                sb.AppendLine();
            }

            sb.AppendLine();


            // Geral dos servidores
            var minDataAgeMinutes = sd.DataAgeMinutes;
            if (minDataAgeMinutes > 70)
            {
                sb.AppendFormat("Idade mínima dos dados: {0}min !!!", minDataAgeMinutes);
                sb.AppendLine();
                mailPriority = Max(mailPriority, MailPriority.High);
            }

            var maxClansUpdatedOnLastHour = sd.ClansWithPlayersUpdatedOnLastHour;
            if (maxClansUpdatedOnLastHour < 4)
            {
                sb.AppendFormat("Clãs atualizados na última hora: {0}", maxClansUpdatedOnLastHour);
                sb.AppendLine();
                mailPriority = Max(mailPriority, MailPriority.High);
            }

            var playersPerHour = (maxPerDay + minPerDay) / 2 / 24;

            sb.AppendLine("No Banco de Dados");
            sb.AppendFormat("Jogadores:          {0:N0}", dd.TotalPlayers);
            sb.AppendLine();

            sb.AppendFormat("Jogadores na fila:  {0:N0} ({1:P1})", dd.PlayersQueueLength,
                dd.PlayersQueueLength * 1.0 / dd.TotalPlayers);
            if (dd.PlayersQueueLength > playersPerHour + 120)
            {
                mailPriority = Max(mailPriority, MailPriority.Normal);
                sb.Append(" !");
            }
            else if (dd.PlayersQueueLength > playersPerHour + 240)
            {
                mailPriority = Max(mailPriority, MailPriority.High);
                sb.Append(" !!!!");
            }

            sb.AppendLine();

            sb.AppendFormat("Membership na fila: {0:N0} ({1:P1})", dd.MembershipQueueLength,
                dd.MembershipQueueLength * 1.0 / dd.TotalEnabledClans);
            if (dd.MembershipQueueLength > 100 * 4 * 2)
            {
                sb.AppendLine(" !!!");
                mailPriority = Max(mailPriority, MailPriority.High);
            }
            else if (dd.MembershipQueueLength > 100 * 4)
            {
                sb.AppendLine(" !");
                mailPriority = Max(mailPriority, MailPriority.Normal);
            }
            else
            {
                sb.AppendLine();
            }


            sb.AppendFormat("Cálculos na fila:   {0:N0} ({1:P1})", dd.CalculateQueueLength,
                dd.CalculateQueueLength * 1.0 / dd.TotalEnabledClans);
            if (dd.CalculateQueueLength > (dd.TotalEnabledClans / 2))
            {
                sb.AppendLine(" !!!");
                mailPriority = Max(mailPriority, MailPriority.High);
            }
            else
            {
                sb.AppendLine();
            }

            sb.AppendFormat("Jogadores agendados/efetivos por dia: {0:N0} / {1:N0} ({2:P1})", dd.ScheduledPlayersPerDay,
                dd.AvgPlayersPerHourLastDay * 24.0,
                dd.ScheduledPlayersPerDay * 1.0 / (dd.AvgPlayersPerHourLastDay * 24.0));

            if (dd.ScheduledPlayersPerDay > maxPerDay)
            {
                mailPriority = Max(mailPriority, MailPriority.Normal);
                sb.AppendLine(" !");
                sb.AppendFormat("Faltam {0:N0} vagas para coleta de dados. Balanceamento será tentado entre {1} e {2}.",
                    dd.ScheduledPlayersPerDay - maxPerDay, minPerDay, maxPerDay);
                sb.AppendLine();
            }
            else if (dd.ScheduledPlayersPerDay < minPerDay)
            {
                mailPriority = Max(mailPriority, MailPriority.Normal);
                sb.AppendLine(" !");
                sb.AppendFormat("Sobram {0:N0} vagas para coleta de dados. Balanceamento será tentado entre {1} e {2}.",
                    minPerDay - dd.ScheduledPlayersPerDay, minPerDay, maxPerDay);
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine();
            }

            sb.AppendFormat("Jogadores agendados/efetivos por hora: {0:N0} - {1:N0} - {2:N0} - {3:N0} - {4:N0}",
                dd.ScheduledPlayersPerHour,
                dd.AvgPlayersPerHourLastDay, dd.AvgPlayersPerHourLast6Hours, dd.AvgPlayersPerHourLast2Hours,
                dd.AvgPlayersPerHourLastHour);

            if (dd.AvgPlayersPerHourLastHour > playersPerHour + 20)
            {
                sb.AppendLine(" !");
                mailPriority = Max(mailPriority, MailPriority.Normal);
            }
            else if (dd.AvgPlayersPerHourLastHour > playersPerHour + 40)
            {
                sb.AppendLine(" !!!");
                mailPriority = Max(mailPriority, MailPriority.High);
            }
            else if (Math.Abs(dd.ScheduledPlayersPerDay - dd.AvgPlayersPerHourLastDay * 24.0) > playersPerHour + 40)
            {
                sb.AppendLine(" !");
                mailPriority = Max(mailPriority, MailPriority.Normal);
                sb.AppendFormat("Diferença de {0:N0}/dia jogadores entre coleta e agendamento. Melhor investigar.",
                    Math.Abs(dd.ScheduledPlayersPerDay - dd.AvgPlayersPerHourLastDay * 24.0));
                sb.AppendLine();
            }
            else if (dd.AvgPlayersPerHourLast6Hours > playersPerHour + 20)
            {
                sb.AppendLine(" !");
                mailPriority = Max(mailPriority, MailPriority.Normal);
            }
            else
            {
                sb.AppendLine();
            }

            // Delay block
            sb.Append($"Delay real nas últimas 48h/72h/96h: {dd.Last48HDelay} - {dd.Last72HDelay} - {dd.Last96HDelay}");
            if (dd.Last48HDelay > 27)
            {
                sb.Append(" !!!");
                mailPriority = Max(mailPriority, MailPriority.High);
            }
            else if (dd.Last48HDelay > 26)
            {
                sb.Append(" !!");
                mailPriority = Max(mailPriority, MailPriority.Normal);
            }
            else if (dd.Last48HDelay < 23)
            {
                sb.Append(" !!!");
                mailPriority = Max(mailPriority, MailPriority.High);
            }
            else if (dd.Last48HDelay < 24)
            {
                sb.Append(" !!");
                mailPriority = Max(mailPriority, MailPriority.Normal);
            }

            sb.AppendLine();

            return sb.ToString();
        }

        private static MailPriority Max(MailPriority a, MailPriority b)
        {
            if (a == b) return a;

            if (a == MailPriority.High) return MailPriority.High;
            if (b == MailPriority.High) return MailPriority.High;

            if (a == MailPriority.Normal && b == MailPriority.High) return MailPriority.High;
            if (b == MailPriority.Normal && a == MailPriority.High) return MailPriority.High;

            if (a == MailPriority.Normal && b == MailPriority.Low) return MailPriority.Normal;
            if (b == MailPriority.Normal && a == MailPriority.Low) return MailPriority.Normal;

            if (a == MailPriority.Low && b != MailPriority.Low) return b;
            if (b == MailPriority.Low && a != MailPriority.Low) return a;

            throw new InvalidOperationException($"Errei algo no fluxo entre {a} e {b}.");
        }

        /// <summary>
        ///     Manda uma mensagem genérica, assincronamente
        /// </summary>
        private void Send(MailMessage message, bool async = true)
        {
            var client = new SmtpClient(_host, _port)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Credentials = new NetworkCredential(_userName, _password)
            };
            if (_useSsl) client.EnableSsl = true;

            if (async)
                Task.Run(() => Execute(() =>
                {
                    Log.Debug("Sending async mail...");
                    client.Send(message);
                    Log.Debug("Sent async mail.");
                }));
            else
                Execute(() =>
                {
                    Log.Debug("Sending sync mail...");
                    client.Send(message);
                    Log.Debug("Sent sync mail.");
                });
        }

        [SuppressMessage("ReSharper", "UnusedParameter.Local")]
        private static void Execute(Action action, int maxTries = 5, bool throwOnFinalError = false)
        {
            Exception lastException = null;
            for (var i = 0; i < maxTries; ++i)
                try
                {
                    action();
                    return;
                }
                catch (Exception ex)
                {
                    if (i < maxTries - 1)
                    {
                        Log.Warn(ex);
                        Log.Warn("...esperando antes de tentar novamente.");
                        Thread.Sleep(TimeSpan.FromSeconds(i * i * 2));
                    }
                    else
                    {
                        Log.Error(ex);
                    }

                    lastException = ex;
                }

            if (lastException != null && throwOnFinalError) throw lastException;
        }
    }
}