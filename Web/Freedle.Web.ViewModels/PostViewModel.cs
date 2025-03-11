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

        public string Content { get; set; }

        public string ImageUrl { get; set; } // Снимка на поста

        public string CreatedOn { get; set; } 

        public string AuthorId { get; set; }

        public string AuthorName { get; set; }

        public string AuthorProfilePictureUrl { get; set; }

        public int LikeCount { get; set; }

        public bool IsLikedByCurrentUser { get; set; }

        public List<CommentViewModel> Comments { get; set; } = new List<CommentViewModel>();
    }

}
