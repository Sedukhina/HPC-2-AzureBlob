using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Azure;

namespace Blob.Pages
{
    public class UploadModel : PageModel
    {
        // Properties to bind form
        [BindProperty]
        public IFormFile? File { get; set; }
        [BindProperty]
        public string Name { get; set; } = "";
        [BindProperty]
        public string Description { get; set; } = "";
        [BindProperty]
        public string Tags { get; set; } = "";

        // Azure Container
        private const string ContainerName = "gallery";
        private readonly BlobContainerClient _containerClient;

        public UploadModel(BlobServiceClient blobServiceClient)
        {
            _containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
            _containerClient.CreateIfNotExistsAsync();
        }

        // Holds the images to show in the gallery
        public List<ImageInfo> GalleryImages { get; set; } = new();

        // Load gallery with metatags
        public async Task OnGetAsync()
        {
            await foreach (var blobItem in _containerClient.GetBlobsAsync(traits: BlobTraits.Metadata, states: BlobStates.Snapshots))
            {
                var blobClient = _containerClient.GetBlobClient(blobItem.Name);

                // If it's a snapshot, append the snapshot query to URI
                var uri = blobItem.Snapshot != null
                    ? blobClient.WithSnapshot(blobItem.Snapshot).Uri.ToString()
                    : blobClient.Uri.ToString();

                string name = blobItem.Metadata.TryGetValue("name", out var n) ? n : "";
                string description = blobItem.Metadata.TryGetValue("description", out var d) ? d : "";
                string tags = blobItem.Metadata.TryGetValue("tags", out var t) ? t : "";

                GalleryImages.Add(new ImageInfo
                {
                    BlobName = blobItem.Name,
                    Url = uri,
                    Name = name,
                    Description = description,
                    Tags = tags,
                    IsSnapshot = blobItem.Snapshot != null,
                    SnapshotTime = blobItem.Snapshot
                });
            }
        }

        // Save new blob from form
        public async Task<IActionResult> OnPostAsync()
        {
            if (File == null || File.Length == 0)
            {
                ModelState.AddModelError("File", "Please select a file.");
                await OnGetAsync();
                return Page();
            }

            var blobName = Guid.NewGuid().ToString() + Path.GetExtension(File.FileName);
            var blobClient = _containerClient.GetBlobClient(blobName);

            using var stream = File.OpenReadStream();

            await blobClient.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = File.ContentType
                },
                Metadata = new Dictionary<string, string>
            {
                { "name", Name },
                { "description", Description },
                { "tags", Tags }
            }
            });

            // Reload and stay on the same page
            return RedirectToPage(); 
        }

        public async Task<IActionResult> OnPostDeleteAsync(string blobName, bool isSnapshot, string? snapshotTime)
        {
            var blobClient = _containerClient.GetBlobClient(blobName);

            if (isSnapshot && !string.IsNullOrEmpty(snapshotTime))
            {
                // Delete only the specific snapshot
                var snapshotClient = blobClient.WithSnapshot(snapshotTime);
                await snapshotClient.DeleteIfExistsAsync();
            }
            else
            {
                // Delete base blob and all snapshots
                await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDuplicateAsync(string blobName)
        {
            if (string.IsNullOrEmpty(blobName))
            {
                return BadRequest();
            }
            var sourceBlob = _containerClient.GetBlobClient(blobName);

            if (!await sourceBlob.ExistsAsync())
            {
                return NotFound();
            }

            // Generate new name for the duplicate
            var newBlobName = Guid.NewGuid().ToString() + Path.GetExtension(blobName);
            var destinationBlob = _containerClient.GetBlobClient(newBlobName);

            // Start the copy operation
            await destinationBlob.StartCopyFromUriAsync(sourceBlob.Uri);

            // Copy metadata too
            var sourceProps = await sourceBlob.GetPropertiesAsync();
            if (sourceProps.Value.Metadata != null && sourceProps.Value.Metadata.Count > 0)
            {
                await destinationBlob.SetMetadataAsync(sourceProps.Value.Metadata);
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSnapshotAsync(string blobName)
        {
            if (string.IsNullOrEmpty(blobName))
            {
                return BadRequest();
            }
            var blobClient = _containerClient.GetBlobClient(blobName);

            try
            {
                var snapshotResponse = await blobClient.CreateSnapshotAsync();

                // Optional: log or store the snapshot URI or metadata
                var snapshotUri = blobClient.WithSnapshot(snapshotResponse.Value.Snapshot).Uri;
                Console.WriteLine($"Snapshot created: {snapshotUri}");

                // You can return this info to the user or store it if needed
            }
            catch (RequestFailedException ex)
            {
                // Handle error
                Console.WriteLine($"Snapshot failed: {ex.Message}");
            }

            return RedirectToPage();
        }
    }
}