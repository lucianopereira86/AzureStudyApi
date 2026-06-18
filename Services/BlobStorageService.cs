using Azure.Storage.Blobs;

namespace AzureStudyApi.Services
{
    public class BlobStorageService
    {
        private readonly BlobContainerClient _container;

        public BlobStorageService(IConfiguration config)
        {
            _container = new BlobContainerClient(config["StorageConnection"], "documents");
        }

        public async Task UploadAsync(string fileName, Stream stream)
        {
            await _container.UploadBlobAsync(fileName, stream);
        }

        public async Task<List<string>> ListAsync()
        {
            var files = new List<string>();

            await foreach(var blob in _container.GetBlobsAsync())
            {
                files.Add(blob.Name);
            }

            return files;
        }
    }
}
