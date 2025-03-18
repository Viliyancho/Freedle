using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Freedle.Web.ViewModels
{
    public class UserViewModel
    {
        public string Id { get; set; }

        public string Username { get; set; }

        public string ProfilePictureUrl { get; set; }

        public bool IsFollowing { get; set; }
    }
}
