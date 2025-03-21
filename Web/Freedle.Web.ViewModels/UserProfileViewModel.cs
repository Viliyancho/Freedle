﻿using Freedle.Data.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Freedle.Web.ViewModels
{
    public class UserProfileViewModel
    {
        public string Username { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string ProfilePictureUrl { get; set; }

        public Gender? Gender { get; set; }

        public DateTime? BirthDay { get; set; }

        public string Description { get; set; }

        public string City { get; set; }

        public string Country { get; set; }

        public int FollowerCount { get; set; }

        public int FollowingCount { get; set; }

        public bool IsFollowing { get; set; }


        public string CurrentUserId { get; set; }

        public string ProfileUserId { get; set; }

        public ICollection<PostViewModel> Posts { get; set; } = new HashSet<PostViewModel>();
    }

}
