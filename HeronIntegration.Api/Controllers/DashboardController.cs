using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.StepProcessors;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using SharpCompress.Common;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IBatchRepository _batchRepo;

    public DashboardController(
        IBatchRepository batchRepo
        )
    {
        _batchRepo = batchRepo;
    }

    [HttpGet("")]
    public async Task<DashboardResponse> GetDashboard()
    {
        try
        {
            var todayBatches = await _batchRepo.GetTodayAsync();

            var result = new DashboardResponse();

            foreach (var batch in todayBatches)
            {
                var item = await _batchRepo.BuildBatchDashboard(batch);

                if (batch.Status == BatchStatus.Running)
                    result.ActiveBatches.Add(item);
                else
                    result.CompletedBatches.Add(item);
            }

            return result;

        }
        catch(Exception e)
        {
            return new DashboardResponse();
        }
    }

}
