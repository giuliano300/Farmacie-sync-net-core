using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class BatchReportService : IBatchReportService
{
    public async Task SaveBatchReportAsync(BatchReport report, List<ExportExecution> exportExecutions)
    {
        Directory.CreateDirectory("batch_reports");

        var file = $"batch_reports/batch_{report.BatchId}.json";

        var productReports = new List<ProductReport>();

        foreach (var e in exportExecutions)
        {
            var status =
                (ExportStatus)e.Status;

            var p = new ProductReport()
            {
                Aic = e.Aic,

                // stringa con nome enum
                Status = status.ToString(),

                Message =
                (
                    status switch
                    {
                        ExportStatus.Pending =>
                            "In attesa",

                        ExportStatus.Insert =>
                            "Prodotto inserito",

                        ExportStatus.Update =>
                            "Prodotto aggiornato",

                        ExportStatus.UpdatePrice =>
                            "Prezzo aggiornato",

                        ExportStatus.InsertImages =>
                            "Immagini inserite",

                        ExportStatus.Success =>
                            "Operazione riuscita",

                        ExportStatus.Error =>
                            e.ErrorMessage,

                        _ =>
                            "Stato sconosciuto"
                    }
                )
                +
                (
                    e.LastAttemptAt.HasValue
                        ? " in data " +
                            e.LastAttemptAt.Value.ToString("dd/MM/yyyy HH:mm:ss")
                        : ""
                )
            };

            productReports.Add(p);
        }
        report.reportProducts = productReports;

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(file, json);
    }
}
