// ReSharper disable VirtualMemberCallInConstructor
namespace Freedle.Data.Models
{
    using System;
    using System.Collections.Generic;

    using Freedle.Data.Common.Models;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;

    public class ApplicationUser : IdentityUser, IAuditInfo, IDeletableEntity
    {
        public ApplicationUser()
        {
            this.Id = Guid.NewGuid().ToString();
            this.Roles = new HashSet<IdentityUserRole<string>>();
            this.Claims = new HashSet<IdentityUserClaim<string>>();
            this.Logins = new HashSet<IdentityUserLogin<string>>();

            this.Posts = new HashSet<Post>();
            this.Comments = new HashSet<Comment>();
            this.UserPages = new HashSet<Page>();
            this.Likes = new HashSet<UserLike>();

            this.Followers = new HashSet<UserFollower>();
            this.Following = new HashSet<UserFollower>();
        }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string ProfilePictureURL { get; set; }

        public DateTime? BirthDay { get; set; }

        public Gender? Gender { get; set; }

        public string Description { get; set; }

        public string City { get; set; }

        public string Country { get; set; }

        public ICollection<Post> Posts { get; set; }

        public ICollection<Comment> Comments { get; set; }

        public ICollection<Page> UserPages { get; set; }

        public ICollection<UserLike> Likes { get; set; }

        public ICollection<UserFollower> Followers { get; set; }

        public ICollection<UserFollower> Following { get; set; }

        public ICollection<Message> Messages { get; set; } = new HashSet<Message>();

        public ICollection<UserConversation> UserConversations { get; set; } = new HashSet<UserConversation>();
        public DateTime CreatedOn { get; set; }

        public DateTime? ModifiedOn { get; set; }

        // Deletable entity
        public bool IsDeleted { get; set; }

        public DateTime? DeletedOn { get; set; }

        public virtual ICollection<IdentityUserRole<string>> Roles { get; set; }

        public virtual ICollection<IdentityUserClaim<string>> Claims { get; set; }

        public virtual ICollection<IdentityUserLogin<string>> Logins { get; set; }
    }
}
