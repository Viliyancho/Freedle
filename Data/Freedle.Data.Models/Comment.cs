namespace Freedle.Data.Models
{
    using System;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Comment
    {
        public int Id { get; set; }

        public int AuthorId { get; set; }

        public ApplicationUser Author { get; set; }

        public DateTime PostedOn { get; set; } = DateTime.UtcNow;

        public string CommentText { get; set; }

        [ForeignKey("Post")]
        public int PostId { get; set; }

        public Post Post { get; set; }
    }
}