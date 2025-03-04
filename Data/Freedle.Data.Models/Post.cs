namespace Freedle.Data.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class Post
    {
        public Post()
        {
            this.Comments = new HashSet<Comment>();
            this.PostReports = new HashSet<PostReport>();
            this.Likes = new HashSet<UserLike>();
        }

        public int Id { get; set; }

        public string Title { get; set; }

        public string Content { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        public ICollection<UserLike> Likes { get; set; }

        public string UserId { get; set; }

        public ApplicationUser User { get; set; }

        public ICollection<Comment> Comments { get; set; }

        public ICollection<PostReport> PostReports { get; set; }
    }
}
