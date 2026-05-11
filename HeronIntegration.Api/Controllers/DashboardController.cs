using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.StepProcessors;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using SharpCompress.Common;
using System.Text.Json;
using static System.Net.WebRequestMethods;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IBatchRepository _batchRepo;
    private readonly HttpClient _http;
    private readonly MongoContext _context;

    public DashboardController(
        HttpClient http,
        MongoContext context,
        IBatchRepository batchRepo
        )
    {
        _http = http;
        _context = context;
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


    [HttpGet("get-reindex-status")]
    public async Task<ReindexStatus> ReadReindexAsync(string batchId)
    {
        var batchExecutions = await _context.BatchExecutions
            .Find(a => a.Id == ObjectId.Parse(batchId))
            .FirstOrDefaultAsync();
        if(batchExecutions == null)
            return new ReindexStatus();

        var customer = await _context.Customers
            .Find(a => a.Id == batchExecutions.CustomerId)
            .FirstOrDefaultAsync(); 

        if (customer == null)
            return new ReindexStatus();

        var BaseUrl = customer.Magento!.BaseUrl;

        try
        {
            var url = $"{BaseUrl}/rest/V1/heron/reindex-status/{batchId}";

            var response = await _http.GetStringAsync(url);

            Console.WriteLine(response);

            var innerJson =
                JsonSerializer.Deserialize<string>(response);

            var result =
                JsonSerializer.Deserialize<ReindexStatus>(
                    innerJson,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }
                );

            return result ?? new ReindexStatus();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);

            return new ReindexStatus();
        }
    }



}
