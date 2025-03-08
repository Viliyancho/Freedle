namespace Freedle.Data.Models
{
    using Freedle.Data.Common.Models;
    using System;
    using System.ComponentModel.DataAnnotations.Schema;

    public class PostReport : BaseDeletableModel<int>
    {
        public string AuthorId { get; set; }

        public ApplicationUser Author { get; set; }

        [ForeignKey("Post")]
        public int PostId { get; set; }

        public Post Post { get; set; }

        public ReportReason ReportReason { get; set; }

        public DateTime ReportedOn { get; set; } = DateTime.UtcNow;
    }
}