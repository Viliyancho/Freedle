namespace Freedle.Data.Models
{
    using Freedle.Data.Common.Models;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Comment : BaseDeletableModel<int>
    {
        public string AuthorId { get; set; }

        public ApplicationUser Author { get; set; }

        public DateTime PostedOn { get; set; } = DateTime.UtcNow;

        public string CommentText { get; set; }

        [ForeignKey("Post")]
        public int PostId { get; set; }

        public Post Post { get; set; }

        // Добавено поле за parent comment (само ако е reply)
        public int? ParentCommentId { get; set; }

        public Comment ParentComment { get; set; }

        public ICollection<Comment> Replies { get; set; } = new HashSet<Comment>();
    }
}
