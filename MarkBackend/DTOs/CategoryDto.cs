using System.ComponentModel.DataAnnotations;

namespace MarkBackend.DTOs
{
    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;

        /// <summary>
        /// 0 means this is a top-level category with no parent.
        /// </summary>
        public int ParentId { get; set; }
    }

    public class CategoryWriteDto
    {
        [Required]
        [StringLength(50, ErrorMessage = "Name cannot exceed 50 characters.")]
        public string Name { get; set; } = null!;

        public int ParentId { get; set; } = 0;
    }
}