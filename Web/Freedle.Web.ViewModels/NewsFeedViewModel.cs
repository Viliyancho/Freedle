using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Freedle.Web.ViewModels
{
    public class NewsFeedViewModel
    {
        public List<PostViewModel> Posts { get; set; } = new List<PostViewModel>();

        public List<UserProfileViewModel> SuggestedUsers { get; set; } = new List<UserProfileViewModel>();
    }

}
