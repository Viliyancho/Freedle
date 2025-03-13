using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Freedle.Web.ViewModels
{
    public class CommentViewModel
    {
        public int Id { get; set; }

        public string AuthorId { get; set; }

        public string AuthorName { get; set; }

        public string AuthorProfilePictureUrl { get; set; }

        public string CommentText { get; set; }

        public string PostedOn { get; set; }

        public int? ParentCommentId { get; set; }

        public List<CommentViewModel> Replies { get; set; } = new List<CommentViewModel>();
    }

}
