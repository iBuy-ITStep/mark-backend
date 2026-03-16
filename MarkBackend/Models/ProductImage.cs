namespace MarkBackend.Models
{
    /// <summary>
    /// Stores product images as binary blobs in the database.
    /// A product has one preview image (IsPreview = true, auto-resized to 800x800 @ 75% JPEG)
    /// and up to 10 description images (IsPreview = false, stored as uploaded).
    /// </summary>
    public class ProductImage
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Nullable — an image may be uploaded before its product is saved (e.g. during creation).
        /// </summary>
        public int? ProductId { get; set; }
        public Product? Product { get; set; }

        public string UploadedById { get; set; } = null!;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// True = this is the card/tooltip preview image (resized on upload).
        /// False = this is an image embedded inside the product description.
        /// </summary>
        public bool IsPreview { get; set; }

        /// <summary>
        /// MIME type, e.g. "image/jpeg". Used when serving the file.
        /// </summary>
        public string ContentType { get; set; } = null!;

        public string OriginalFileName { get; set; } = null!;

        public byte[] Data { get; set; } = null!;

        // TODO: Implement the orphan-cleanup SQL Server job, add a query:
        // DELETE FROM ProductImages WHERE ProductId IS NULL AND UploadedAt < DATEADD(day, -1, GETUTCDATE())
        // This will handle images uploaded during product creation that were never linked.
    }
}