using HeronIntegration.Engine.External.Farmadati;
using HeronIntegration.Engine.External.Farmadati.FullImportNew;
using HeronIntegration.Engine.External.Farmadati.Interfaces;
using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using ServiceReference1;
using System.IO.Compression;
using System.ServiceModel.Channels;
using System.Text.Json;
using System.Text.Json.Serialization;

[ApiController]
[Route("api/test/farmadati")]
public class FarmadatiTestController : ControllerBase
{
    private readonly MongoContext _context;
    private readonly IProductBaseInfoProvider _provider;
    private readonly IProductLongDescriptionProvider _longProvider;
    private readonly IProductImageProvider _imgProvider;
    private readonly FarmadatiItaliaWebServicesM2Client _client;
    private readonly IConfiguration _configuration;
    private readonly IFarmadatiFullImportJob _importJob;


    public FarmadatiTestController(MongoContext context, IProductBaseInfoProvider provider, 
        IProductLongDescriptionProvider longProvider, IProductImageProvider imgProvider, IConfiguration configuration, IFarmadatiFullImportJob importJob)
    {
        _context = context;
        _provider = provider;
        _longProvider = longProvider;
        _imgProvider = imgProvider;
        _importJob = importJob;
        _client = new FarmadatiItaliaWebServicesM2Client(
               FarmadatiItaliaWebServicesM2Client.EndpointConfiguration
                   .BasicHttpBinding_FarmadatiItaliaWebServicesM2);
        _configuration = configuration;
    }



    [HttpGet("datasets")]
    public async Task<IActionResult> GetDatasets()
    {
        try
        {
            var username = _configuration["Farmadati:Username"];
            var password = _configuration["Farmadati:Password"];

            var result = await _client.GetDataSetAsync(
                username,
                password,
                "TE001",
                "COUNT",
                1);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                Error = ex.Message,
                InnerException = ex.InnerException?.Message
            });
        }
    }

    [HttpGet("datasets/{schema}")]
    public async Task<IActionResult> GetSchemaDatasets(string schema)
    {
        try
        {
            var username = _configuration["Farmadati:Username"];
            var password = _configuration["Farmadati:Password"];

            var result = await _client.GetSchemaDataSetAsync(
                username,
                password,
                schema,
            false);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                Error = ex.Message,
                InnerException = ex.InnerException?.Message
            });
        }
    }

    [HttpGet("download/{dataset}")]
    public async Task<IActionResult> Download(string dataset)
    {
        var username = _configuration["Farmadati:Username"];
        var password = _configuration["Farmadati:Password"];

        var result = await _client.GetDataSetAsync(
            username,
            password,
            dataset,
            "GETRECORDS",
            1);

        if (result.CodEsito != "OK")
            return BadRequest(result);

        var folder = Path.Combine(
            @"C:\Farmadati",
            dataset);

        Directory.CreateDirectory(folder);

        var zipPath = Path.Combine(
            folder,
            $"{dataset}.zip");


        await System.IO.File.WriteAllBytesAsync(
            zipPath,
            result.ByteListFile);

        ZipFile.ExtractToDirectory(
            zipPath,
            Path.Combine(folder, "Extracted"),
            true);

        return Ok(new
        {
            result.CodEsito,
            result.DescEsito,
            ZipPath = zipPath
        });
    }


    [HttpGet("")]
    public async Task<IActionResult> GetBaseInfo()
    {
        var code = "033262027";
        var result = await _provider.GetBaseInfoAsync(code);
        var description = await _longProvider.GetLongDescriptionAsync(code);
        var img = await _imgProvider.GetImagesAsync(code);

        if (result == null)
            return NotFound($"Prodotto {code} non trovato");

        return Ok(img);
    }

    [HttpGet("UpdateSpecificFields")]
    public async Task UpdateSpecificFields()
    {
        var items = await _context.FarmadatiCaches
          .Find(x => string.IsNullOrEmpty(x.MacroGroup))
         .ToListAsync();

        var semaphore = new SemaphoreSlim(5); // max 5 parallele
        var tasks = new List<Task>();
        int i = 0;
        foreach (var item in items)
        {
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();

                try
                {
                    var code = item.Aic;
                    var result = await _provider.GetBaseInfoAsync(code);

                    if (result == null)
                        return;

                    var macroGroup = result.ProductTypeCode;

                    if (!string.IsNullOrEmpty(macroGroup))
                    {
                        var update = Builders<FarmadatiCache>.Update
                            .Set(x => x.MacroGroup, macroGroup);

                        await _context.FarmadatiCaches.UpdateOneAsync(
                            x => x.Id == item.Id,
                            update
                        );
                    }

                    i++;
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);
    }



    public class ProductMongo
    {
        [JsonPropertyName("ProductName")]
        public string ProductName { get; set; }

        [JsonPropertyName("CustomerId")]
        public string CustomerId { get; set; }

        [JsonPropertyName("Aic")]
        public string Aic { get; set; }
    }


    [HttpPost("import-full")]
    public async Task<IActionResult> ImportFull()
    {
        try
        {
            await _importJob.ExecuteAsync();

            return Ok(new
            {
                Success = true,
                Message = "Import Farmadati completato"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                Success = false,
                ex.Message,
                InnerException = ex.InnerException?.Message
            });
        }
    }

};