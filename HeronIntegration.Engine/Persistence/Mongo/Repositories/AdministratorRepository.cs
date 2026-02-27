using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class AdministratorRepository : IAdministratorRepository
{
    private readonly MongoContext _context;
    private readonly IBatchReportRepository _batchReport;

    public AdministratorRepository(MongoContext context, IBatchReportRepository batchReport)
    {
        _context = context;
        _batchReport = batchReport;
    }

    public async Task<Administrator?> Login(Login l)
    {
        var user = await _context.Administrators
            .Find(p => p.pwd == l.password && p.email == l.email)
            .FirstOrDefaultAsync();

        if (user == null)
            return null;

        var update = Builders<Administrator>.Update
            .Set(x => x.lastLogin, DateTime.UtcNow);

        await _context.Administrators
            .UpdateOneAsync(x => x.Id == user.Id, update);

        user.lastLogin = DateTime.UtcNow;

        return user;
    }
}
