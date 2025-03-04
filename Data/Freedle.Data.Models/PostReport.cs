namespace Freedle.Data.Models
{
    using System;
    using System.ComponentModel.DataAnnotations.Schema;

    public class PostReport
    {
        public int Id { get; set; }

        public string AuthorId { get; set; }

        public ApplicationUser Author { get; set; }

        [ForeignKey("Post")]
        public int PostId { get; set; }

        public Post Post { get; set; }

        public ReportReason ReportReason { get; set; }

        public DateTime ReportedOn { get; set; } = DateTime.UtcNow;
    }
}