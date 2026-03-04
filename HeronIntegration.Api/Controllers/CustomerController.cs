using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

[ApiController]
[Route("api/admin/customers")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerRepository _repo;
    private readonly IBatchRepository _batchRepo;
    private readonly IStepRepository _stepRepo;
    private static readonly Dictionary<string, int> stepOrder = new()
{
    { "HeronImport", 1 },
    { "Farmadati", 2 },
    { "Suppliers", 3 },
    { "Magento", 4 }
};

    public CustomersController(ICustomerRepository repo, IBatchRepository batchRepo, IStepRepository stepRepo)
    {
        _repo = repo;
        _batchRepo = batchRepo;
        _stepRepo = stepRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var customers = await _repo.GetAllAsync();

        var result = new List<CustomerWithBatchStatus>();


        foreach (var customer in customers)
        {
            bool canStartNewBatch = true;

            var runningBatch = await _batchRepo.GetRunningBatchAsync(customer.Id);

            StepExecution? step = null;

            if (runningBatch != null)
            {
                var steps = await _stepRepo.GetByBatchAsync(runningBatch.Id.ToString());

                var orderedSteps = steps
                    .OrderBy(x => stepOrder.ContainsKey(x.Step) ? stepOrder[x.Step] : int.MaxValue)
                    .ToList();

                step = orderedSteps
                    .FirstOrDefault(x => x.Status != StepStatus.Success);

                canStartNewBatch = step == null;
            }

            result.Add(new CustomerWithBatchStatus
            {
                Customer = customer,
                CanStartNewBatch = canStartNewBatch,
                RunningBatchId = runningBatch?.Id.ToString(),
                RunningStepId = step?.Id.ToString(),
                CurrentStep = step?.Step,
                StepStatus = step?.Status
            });
        }

        return Ok(result);
    }
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var customer = await _repo.GetByIdAsync(id);

        if (customer == null)
            return NotFound();

        bool canStartNewBatch = true;

        var runningBatch = await _batchRepo.GetRunningBatchAsync(customer.Id);

        StepExecution? step = null;

        if (runningBatch != null)
        {
            var steps = await _stepRepo.GetByBatchAsync(runningBatch.Id.ToString());

            var orderedSteps = steps
                .OrderBy(x => stepOrder.ContainsKey(x.Step) ? stepOrder[x.Step] : int.MaxValue)
                .ToList();

            step = orderedSteps
                .FirstOrDefault(x => x.Status != StepStatus.Success);

            canStartNewBatch = step == null;

        }

        var result = new CustomerWithBatchStatus
        {
            Customer = customer,
            CanStartNewBatch = canStartNewBatch,
            RunningBatchId = runningBatch?.Id.ToString(),
            RunningStepId = step?.Id.ToString(),
            CurrentStep = step?.Step,
            StepStatus = step?.Status
        };


        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Customer customer)
    {
        customer.Id = ObjectId.GenerateNewId().ToString();
        await _repo.InsertAsync(customer);
        return Ok(customer);
    }

    [HttpPut("{id}")]
    public async Task<bool> Update(string id, Customer customer)
    {
        try
        {
            customer.Id = id;
            await _repo.UpdateAsync(customer);
            return true;
        }
        catch(Exception e)
        {
            return false;
        }
    }

    [HttpDelete("{id}")]
    public async Task<bool> Delete(string id)
    {
        try
        {
            await _repo.DeleteAsync(id);
            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }
}
