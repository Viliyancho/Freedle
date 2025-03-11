namespace Freedle.Data.Models
{
    using Freedle.Data.Common.Models;
    using Microsoft.AspNetCore.Http;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class Post : BaseDeletableModel<int>
    {
        public Post()
        {
            this.Comments = new HashSet<Comment>();
            this.PostReports = new HashSet<PostReport>();
            this.Likes = new HashSet<UserLike>();
        }

        public string ImageURL { get; set; }

        public string Content { get; set; }

        public int LikeCount { get; set; } = 0;

        public ICollection<UserLike> Likes { get; set; }

        public string UserId { get; set; }

        public ApplicationUser User { get; set; }

        public ICollection<Comment> Comments { get; set; }

        public ICollection<PostReport> PostReports { get; set; }
    }
}
