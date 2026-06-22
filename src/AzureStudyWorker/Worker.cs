using Azure.Messaging.ServiceBus;

namespace AzureStudyWorker;

public class Worker(ILogger<Worker> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = new ServiceBusClient(configuration["ServiceBusConnectionString"]);

        var processor = client.CreateProcessor("orders");

        processor.ProcessMessageAsync += ProcessMessage;

        processor.ProcessErrorAsync += ProcessError;

        await processor.StartProcessingAsync(stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessMessage(ProcessMessageEventArgs args)
    {
        var body = args.Message.Body.ToString();
        logger.LogInformation($"Mensagem recebida: {body}");
        await args.CompleteMessageAsync(args.Message);
        //throw new Exception("Erro simulado no processamento");
    }

    private Task ProcessError(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Erro ao processar mensagem");
        return Task.CompletedTask;
    }
}
