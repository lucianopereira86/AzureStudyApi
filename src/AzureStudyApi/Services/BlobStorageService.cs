
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace AzureStudyApi.Services
{
    public class BlobStorageService
    {
        private readonly BlobServiceClient _serviceClient;
        private readonly BlobContainerClient _container;

        public BlobStorageService(IConfiguration config)
        {
            string accountName = config["StorageAccountName"]!;
            var blobUri = new Uri($"https://{accountName}.blob.core.windows.net");
            _serviceClient = new BlobServiceClient(blobUri, new DefaultAzureCredential());
            _container = _serviceClient.GetBlobContainerClient("documents");
        }

        public async Task UploadAsync(string fileName, Stream stream)
        {
            await _container.UploadBlobAsync(fileName, stream);
        }

        public async Task<List<string>> ListAsync()
        {
            var files = new List<string>();

            await foreach (var blob in _container.GetBlobsAsync())
            {
                files.Add(blob.Name);
            }

            return files;
        }

        public async Task<Uri> GenerateDownloadUrlAsync(string fileName)
        {
            var blobClient = _container.GetBlobClient(fileName);

            var startsOn = DateTimeOffset.UtcNow.AddMinutes(-5);
            var expiresOn = DateTimeOffset.UtcNow.AddMinutes(10);

            var delegationKey = await _serviceClient.GetUserDelegationKeyAsync(new BlobGetUserDelegationKeyOptions(expiresOn) { StartsOn = startsOn });

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _container.Name,
                BlobName = fileName,
                Resource = "b",
                StartsOn = startsOn,
                ExpiresOn = expiresOn
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasQuery = sasBuilder.ToSasQueryParameters(delegationKey, _serviceClient.AccountName);

            return new Uri($"{blobClient.Uri}?{sasQuery}");
        }
    }
}
