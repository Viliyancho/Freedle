using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Freedle.Web.ViewModels
{
    public class PostViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; }

        public string Content { get; set; }

        public string CreatedOn { get; set; } // Ще го форматираме като "преди 5 минути"

        public string AuthorId { get; set; }

        public string AuthorName { get; set; }

        public string AuthorProfilePictureUrl { get; set; }

        public int LikeCount { get; set; }

        public bool IsLikedByCurrentUser { get; set; }

        public List<CommentViewModel> Comments { get; set; } = new List<CommentViewModel>();
    }

}
