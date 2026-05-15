using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace HeronIntegration.Engine.External.Farmadati.Services;

public class ImageStorageService
{
    private readonly GridFSBucket _bucket;

    public ImageStorageService(IMongoDatabase db)
    {
        _bucket = new GridFSBucket(db);
    }

    public async Task<ObjectId> SaveAsync(string fileName, byte[] bytes, string mime)
    {
        var id = ObjectId.GenerateNewId();

        await _bucket.UploadFromBytesAsync(
            id,
            fileName,
            bytes,
            new GridFSUploadOptions
            {
                Metadata = new BsonDocument
                {
                    { "mime", mime }
                }
            });

        return id;
    }

    public async Task<byte[]> GetAsync(ObjectId id)
    {
        return await _bucket.DownloadAsBytesAsync(id);
    }

    public async Task<string> GetBase64Async(ObjectId id)
    {
        var bytes = await _bucket.DownloadAsBytesAsync(id);
        return Convert.ToBase64String(bytes);
    }

    public async Task DownloadToFileAsync(
    ObjectId id,
    string path,
    CancellationToken token = default)
    {
        try
        {
            await using var stream =
                await _bucket.OpenDownloadStreamAsync(
                    id,
                    cancellationToken: token);

            await using var file =
                File.Create(path);

            await stream.CopyToAsync(
                file,
                token);
        }
        catch(Exception e)
        {
            var j = e;
        }
    }

    public async Task CopyToAsync(
    ObjectId id,
    Stream destination,
    CancellationToken token = default)
    {
        await using var stream =
            await _bucket.OpenDownloadStreamAsync(
                id,
                cancellationToken: token);

        await stream.CopyToAsync(
            destination,
            token);
    }
}
