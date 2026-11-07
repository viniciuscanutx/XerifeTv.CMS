using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using XerifeTv.CMS.Modules.Abstractions.Interfaces;
using XerifeTv.CMS.Modules.Common;

namespace XerifeTv.CMS.Modules.Abstractions.Services;

// Upload no Blob Storage da Vertra pela API S3 (bucket público servido pela CDN)
public class StorageFilesService : IStorageFilesService, IDisposable
{
    private readonly string[] _acceptedExtensions = [".vtt", ".xlsx", ".xls"];
    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private readonly string _publicBaseUrl;

    public StorageFilesService(IConfiguration _configuration)
    {
        var section = _configuration.GetSection("BlobStorage");
        _bucket = section["Bucket"] ?? "public";
        _publicBaseUrl = (section["PublicBaseUrl"] ?? string.Empty).TrimEnd('/');

        var config = new AmazonS3Config
        {
            ServiceURL = section["ServiceUrl"] ?? "https://s3.vertracloud.dev",
            AuthenticationRegion = "auto",
            ForcePathStyle = true,
            // Checksums padrão do SDK v4 não são aceitos por todo S3 compatível
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };

        _client = new AmazonS3Client(
          new BasicAWSCredentials(section["AccessKey"] ?? string.Empty, section["SecretKey"] ?? string.Empty),
          config);
    }

    public async Task<Result<string>> UploadFileAsync(Stream fileStream, string fileName, string bucketName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_publicBaseUrl))
                return Result<string>.Failure(new Error("500", "BlobStorage:PublicBaseUrl nao configurado"));

            var fileExtension = Path.GetExtension(fileName);

            if (!_acceptedExtensions.Contains(fileExtension))
                return Result<string>.Failure(new Error("400", $"Extensao de arquivo {fileExtension} invalida"));

            using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms);
            ms.Position = 0;

            // O antigo "bucket" do Supabase vira a pasta dentro do bucket público
            var key = $"{bucketName}/{fileName}";

            await _client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = ms,
                UseChunkEncoding = false
            });

            return Result<string>.Success($"{_publicBaseUrl}/{key}");
        }
        catch (Exception ex)
        {
            var error = new Error("500", ex.InnerException?.Message ?? ex.Message);
            return Result<string>.Failure(error);
        }
    }

    public void Dispose() => _client.Dispose();
}
