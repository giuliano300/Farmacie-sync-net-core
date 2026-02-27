using DnsClient;
using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using MongoDB.Bson;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface IAdministratorRepository
{
    Task<Administrator?> Login(Login l);
}
