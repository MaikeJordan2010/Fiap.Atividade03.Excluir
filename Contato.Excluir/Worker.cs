using Contato.Excluir.Aplicacao;
using Contato.Excluir.Dominio;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace Contato.Excluir
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConnectionFactory _factory;
        private readonly IContatoComandos _contatoComandos;
        private const string _nomeFila = "contato.fila.excluir";

        public Worker(ILogger<Worker> logger, IContatoComandos contatoComandos, IConfiguration config)
        {
            _logger = logger;
            _contatoComandos = contatoComandos;
            _factory = new ConnectionFactory()
            {
                HostName = config.GetSection("RabbitLocal").GetValue<string>("Endereco") ?? "localhost",
                UserName = config.GetSection("RabbitLocal").GetValue<string>("Usuario") ?? "guest",
                Password = config.GetSection("RabbitLocal").GetValue<string>("Senha") ?? "guest",
                Port = config.GetSection("RabbitLocal").GetValue<int>("Porta"),
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (true) {
                try
                {
                    using var connection = await _factory.CreateConnectionAsync();
                    using (var channel = await connection.CreateChannelAsync())
                    {
                        await channel.ExchangeDeclareAsync(exchange: _nomeFila, type: ExchangeType.Fanout);
                        QueueDeclareOk queueDeclareResult = await channel.QueueDeclareAsync();
                        await channel.QueueBindAsync(queue: _nomeFila, exchange: _nomeFila, routingKey: _nomeFila);

                        Console.WriteLine($"OBTENDO DADOS PARA EXCLUS�O: ");

                        while (!stoppingToken.IsCancellationRequested)
                        {
                            var consumer = new AsyncEventingBasicConsumer(channel);
                            consumer.ReceivedAsync += async (modal, resp) =>
                            {
                                byte[] body = resp.Body.ToArray();
                                var message = Encoding.UTF8.GetString(body);

                                if(string.IsNullOrEmpty(message))
                                    await Task.CompletedTask;

                                var contato = JsonConvert.DeserializeObject<DadosContato>(message);

                                if (contato != null)
                                {
                                    Console.WriteLine($"ENVIANDO REGISTRO: {contato.Guid}  DE NOME: {contato.Nome} PARA EXCLUS�O!");
                                    _contatoComandos.Excluir(contato!);
                                }

                                await Task.CompletedTask;
                            };

                            await channel.BasicConsumeAsync(_nomeFila, autoAck: true, consumer: consumer);

                            await Task.Delay(1000, stoppingToken);
                        }

                    }

                }
                catch (Exception ex) 
                { 
                    Console.WriteLine(ex.Message );
                    Thread.Sleep(1000);
                }
            }

        }
        
    }
}
