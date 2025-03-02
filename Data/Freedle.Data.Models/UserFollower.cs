using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Freedle.Data.Models
{
    public class UserFollower
    {
        public string UserId { get; set; }

        public ApplicationUser User { get; set; }

        public string FollowerId { get; set; }

        public ApplicationUser Follower { get; set; }

        public DateTime? FollowedDate { get; set; } = DateTime.UtcNow;

        public DateTime? UnfollowedDate { get; set; }
    }
}
