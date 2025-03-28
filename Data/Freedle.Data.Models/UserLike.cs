﻿namespace Freedle.Data.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;

    public class UserLike
    {
        public string UserId { get; set; }

        public ApplicationUser User { get; set; }

        public int PostId { get; set; }

        public Post Post { get; set; }

        public DateTime LikedOn { get; set; } = DateTime.UtcNow;
    }
}