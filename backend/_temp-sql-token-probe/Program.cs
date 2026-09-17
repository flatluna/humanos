using Azure.Storage.Blobs;

var connStr = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
    ?? throw new InvalidOperationException("Set AZURE_STORAGE_CONNECTION_STRING environment variable.");
var blobServiceClient = new BlobServiceClient(connStr);
var containerClient = blobServiceClient.GetBlobContainerClient("81c35f10-60ab-4ca4-b2ec-6c4bee8d0c0b");
var blobClient = containerClient.GetBlobClient("process-documents/96292aca-3df1-48e3-8b59-634e579f878f/structured/process-structure.json");
if (await blobClient.ExistsAsync())
{
    var content = await blobClient.DownloadContentAsync();
    Console.WriteLine(content.Value.Content.ToString());
}
else
{
    Console.WriteLine("Blob does NOT exist!");
}
