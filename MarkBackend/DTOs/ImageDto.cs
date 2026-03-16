namespace MarkBackend.DTOs
{
    /// <summary>
    /// Returned after a successful image upload.
    /// </summary>
    public class ImageUploadResultDto
    {
        /// <summary>
        /// The image's permanent ID. Embed this in description HTML as:
        /// &lt;img src="/api/images/{Id}"&gt;
        /// </summary>
        public Guid Id { get; set; }
        public bool IsPreview { get; set; }
        public string OriginalFileName { get; set; } = null!;
        public DateTime UploadedAt { get; set; }
    }
}