using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using MarkBackend.DTOs;
using MarkBackend.Interfaces;
using MarkBackend.Models;

namespace MarkBackend.Controllers
{
    /// <summary>
    /// Handles image upload and retrieval. Images are stored as binary blobs in the database.
    /// </summary>
    [ApiController]
    [Route("api/images")]
    [Produces("application/json")]
    public class ImagesController : ControllerBase
    {
        // 8 MB — maximum accepted upload size for any image
        private const int MaxFileSizeBytes = 8 * 1024 * 1024;

        // Reject images whose decoded pixel dimensions exceed this.
        // 8000×8000 @ 4 bytes/pixel = ~256 MB in RAM — well beyond any product image need.
        private const int MaxImageDimension = 8000;

        // Preview images are resized to fit within this box (aspect ratio preserved)
        private const int PreviewMaxDimension = 800;
        private const int PreviewJpegQuality = 75;

        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/webp", "image/gif"
        };

        private readonly IImageRepository _images;

        public ImagesController(IImageRepository images) => _images = images;

        // ── Serve ────────────────────────────────────────────────────────────

        /// <summary>
        /// Serves an image by its ID. No authentication required — images are public.
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid id)
        {
            var image = await _images.GetByIdAsync(id);
            if (image == null) return NotFound();

            // Cache headers — images are immutable once uploaded
            Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return File(image.Data, image.ContentType, image.OriginalFileName);
        }

        /// <summary>
        /// Returns metadata for all description images linked to a product.
        /// The rich-text editor calls this on load so it knows which images already exist.
        /// Does not return the binary data — fetch individual images via GET /api/images/{id}.
        /// </summary>
        /// <param name="productId">The product ID to fetch images for.</param>
        /// <returns>A list of image metadata objects (<see cref="ImageUploadResultDto"/>) for the specified product.</returns>
        [HttpGet("product/{productId:int}")]
        [Authorize(Roles = "Admin,Seller")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByProduct(int productId)
        {
            var images = await _images.GetDescriptionImagesByProductAsync(productId);
            return Ok(images.Select(i => new ImageUploadResultDto
            {
                Id = i.Id,
                IsPreview = i.IsPreview,
                OriginalFileName = i.OriginalFileName,
                UploadedAt = i.UploadedAt
            }));
        }

        /// <summary>
        /// Returns all your uploaded images (both preview and description).
        /// Use this to browse images you've uploaded without linking them to products.
        /// </summary>
        /// <returns>A list of all images uploaded by the authenticated user.</returns>
        [HttpGet("my-images")]
        [Authorize(Roles = "Admin,Seller")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyImages()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var images = await _images.GetImagesByUserAsync(userId);
            return Ok(images.Select(i => new ImageUploadResultDto
            {
                Id = i.Id,
                IsPreview = i.IsPreview,
                OriginalFileName = i.OriginalFileName,
                UploadedAt = i.UploadedAt
            }));
        }

        // ── Upload: description image ─────────────────────────────────────────

        /// <summary>
        /// Uploads a description image (embedded inside rich-text product description).
        /// Returns the image ID to embed as: &lt;img src="/api/images/{id}"&gt;
        /// Max size: 8 MB. Accepted image types: JPEG, PNG, WebP, GIF.
        /// A product may have at most 10 description images — extras are deleted asynchronously.
        /// </summary>
        [HttpPost("upload")]
        [Authorize(Roles = "Admin,Seller")]
        [RequestSizeLimit(MaxFileSizeBytes)]
        [ProducesResponseType(typeof(ImageUploadResultDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Upload(IFormFile file, [FromQuery] int? productId)
        {
            var validation = ValidateFile(file);
            if (validation != null) return BadRequest(validation);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var bytes = await ReadFileBytesAsync(file);

            var image = new ProductImage
            {
                ProductId = productId,
                UploadedById = userId,
                IsPreview = false,
                ContentType = file.ContentType,
                OriginalFileName = Path.GetFileName(file.FileName),
                Data = bytes
            };

            await _images.AddAsync(image);

            return CreatedAtAction(nameof(Get), new { id = image.Id }, new ImageUploadResultDto
            {
                Id = image.Id,
                IsPreview = false,
                OriginalFileName = image.OriginalFileName,
                UploadedAt = image.UploadedAt
            });
        }

        // ── Upload: preview image ─────────────────────────────────────────────

        /// <summary>
        /// Uploads and auto-resizes a product preview image.
        /// The image is scaled to fit within 800×800 pixels at 75% JPEG quality.
        /// Pass the returned ID as PreviewImageId when creating or updating a product.
        /// </summary>
        [HttpPost("upload/preview")]
        [Authorize(Roles = "Admin,Seller")]
        [RequestSizeLimit(MaxFileSizeBytes)]
        [ProducesResponseType(typeof(ImageUploadResultDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadPreview(IFormFile file, [FromQuery] int? productId)
        {
            var validation = ValidateFile(file);
            if (validation != null) return BadRequest(validation);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var originalBytes = await ReadFileBytesAsync(file);
            byte[] resizedBytes;
            try
            {
                resizedBytes = ResizeToPreview(originalBytes);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            var image = new ProductImage
            {
                ProductId = productId,
                UploadedById = userId,
                IsPreview = true,
                // Preview is always stored as JPEG regardless of input format
                ContentType = "image/jpeg",
                OriginalFileName = Path.ChangeExtension(Path.GetFileName(file.FileName), ".jpg"),
                Data = resizedBytes
            };

            await _images.AddAsync(image);

            return CreatedAtAction(nameof(Get), new { id = image.Id }, new ImageUploadResultDto
            {
                Id = image.Id,
                IsPreview = true,
                OriginalFileName = image.OriginalFileName,
                UploadedAt = image.UploadedAt
            });
        }

        // ── Delete ────────────────────────────────────────────────────────────

        /// <summary>
        /// Deletes an image by ID. Admin only.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var image = await _images.GetByIdAsync(id);
            if (image == null) return NotFound();

            await _images.DeleteAsync(id);
            return NoContent();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string? ValidateFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return "No file provided.";

            if (file.Length > MaxFileSizeBytes)
                return $"File exceeds the {MaxFileSizeBytes / 1024 / 1024} MB size limit.";

            if (!AllowedMimeTypes.Contains(file.ContentType))
                return $"Unsupported file type '{file.ContentType}'. Accepted: JPEG, PNG, WebP, GIF.";

            return null;
        }

        private static async Task<byte[]> ReadFileBytesAsync(IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            return ms.ToArray();
        }

        private static byte[] ResizeToPreview(byte[] source)
        {
            using var inputStream = new MemoryStream(source);
            using var image = Image.Load(inputStream);

            // Decompression bomb guard: reject images with absurd decoded dimensions
            if (image.Width > MaxImageDimension || image.Height > MaxImageDimension)
                throw new InvalidOperationException(
                    $"Decoded image dimensions ({image.Width}×{image.Height}) exceed the {MaxImageDimension}px limit.");

            if (image.Width > PreviewMaxDimension || image.Height > PreviewMaxDimension)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(PreviewMaxDimension, PreviewMaxDimension),
                    Mode = ResizeMode.Max
                }));
            }

            using var outputStream = new MemoryStream();
            image.Save(outputStream, new JpegEncoder { Quality = PreviewJpegQuality });
            return outputStream.ToArray();
        }

        /// <summary>
        /// Extracts all image identifier GUIDs referenced in the specified product description.
        /// Called from ProductsController after a product description is saved.
        /// Parses all /api/images/{guid} references and deletes any beyond the 10-image limit.
        /// This is public static so ProductsController can call it without taking a controller dependency.
        /// </summary>
        /// <remarks>
        /// Image references are expected in the format '/api/images/{guid}'. Only valid GUIDs
        /// are returned, and duplicates are removed. This method does not validate the existence of the images; it only
        /// parses identifiers from the description.
        /// </remarks>
        /// <param name="description">The product description text to parse for image references. Can be null or empty.</param>
        /// <returns>An enumerable collection of GUIDs representing the image identifiers found in the description. The
        /// collection will be empty if no valid image references are present.</returns>
        public static IEnumerable<Guid> ExtractDescriptionImageIds(string? description)
        {
            if (string.IsNullOrEmpty(description)) return Enumerable.Empty<Guid>();

            var matches = Regex.Matches(
                description,
                @"/api/images/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})",
                RegexOptions.IgnoreCase);

            return matches
                .Select(m => Guid.TryParse(m.Groups[1].Value, out var g) ? (Guid?)g : null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .Distinct();
        }
    }
}