namespace Blob.Pages
{
    public class ImageInfo
    {
        public string BlobName { get; set; } = "";
        public string Url { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Tags { get; set; } = "";
        public bool IsSnapshot { get; set; } = false;
        public string? SnapshotTime { get; set; }
    }
}
