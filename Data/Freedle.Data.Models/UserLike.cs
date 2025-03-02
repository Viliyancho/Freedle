namespace Freedle.Data.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;

    public class UserLike
    {
        [Key]
        public int UserId { get; set; }

        public ApplicationUser User { get; set; }

        public int PostId { get; set; }

        public Post Post { get; set; }

        public DateTime LikedOn { get; set; } = DateTime.UtcNow;
    }
}