using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/administrator")]
public class AdministratorController : ControllerBase
{
    private readonly IAdministratorRepository _adminRepo;

    public AdministratorController(
        IAdministratorRepository adminRepo
        )
    {
        _adminRepo = adminRepo;
    }

    [HttpPost("login")]
    public async Task<Administrator> Login(Login req)
    {
        var c = await _adminRepo.Login(req);

        return c;
    }
}
