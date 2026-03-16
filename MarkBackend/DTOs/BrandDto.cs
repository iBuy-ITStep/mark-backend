using System.ComponentModel.DataAnnotations;

namespace MarkBackend.DTOs
{
    public class BrandDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    public class BrandWriteDto
    {
        [Required]
        [StringLength(50, ErrorMessage = "Name cannot exceed 50 characters.")]
        public string Name { get; set; } = null!;
    }
}