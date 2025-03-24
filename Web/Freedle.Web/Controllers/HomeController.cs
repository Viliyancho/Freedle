namespace Freedle.Web.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Freedle.Data;
    using Freedle.Data.Models;
    using Freedle.Web.Hubs;
    using Freedle.Web.ViewModels;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.EntityFrameworkCore;
    using SendGrid.Helpers.Mail;

    public class HomeController : BaseController
    {
        private readonly ApplicationDbContext dbContext;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IHubContext<ChatHub> hubContext;

        public HomeController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, IHubContext<ChatHub> hubContext)
        {
            this.dbContext = dbContext;
            this.userManager = userManager;
            this.hubContext = hubContext;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await this.userManager.GetUserAsync(User);
            string currentUserId = currentUser?.Id;

            // Взимаме последните 20 поста
            var posts = this.dbContext.Posts
                .Where(p => !p.IsDeleted)
                .OrderByDescending(p => p.CreatedOn)
                .Take(30)
                .Select(p => new PostViewModel
                {
                    Id = p.Id,
                    Content = p.Content,
                    ImageUrl = p.ImageURL,
                    CreatedOn = p.CreatedOn.ToString("f"),
                    AuthorId = p.User.Id,
                    AuthorName = $"{p.User.FirstName} {p.User.LastName}",
                    AuthorProfilePictureUrl = p.User.ProfilePictureURL,
                    LikeCount = p.LikeCount,
                    IsLikedByCurrentUser = p.Likes.Any(l => l.UserId == currentUserId),
                    Comments = p.Comments.Select(c => new CommentViewModel
                    {
                        Id = c.Id,
                        AuthorId = c.Author.Id,
                        AuthorName = $"{c.Author.FirstName} {c.Author.LastName}",
                        AuthorProfilePictureUrl = c.Author.ProfilePictureURL,
                        CommentText = c.CommentText,
                        PostedOn = c.PostedOn.ToString("f"),
                    }).ToList(),
                })
                .ToList();

            var suggestedUsers = this.dbContext.Users
                .Where(u => u.Id != currentUserId)
                .OrderBy(r => Guid.NewGuid())
                .Take(5)
                .Select(u => new SuggestedUserViewModel
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    ProfilePictureUrl = u.ProfilePictureURL,
                    IsFollowedByCurrentUser = u.Followers.Any(f => f.FollowerId == currentUserId),
                })
                .ToList();

            var model = new NewsFeedViewModel
            {
                Posts = posts,
                SuggestedUsers = suggestedUsers,
            };

            return this.View(model);
        }

        public async Task<IActionResult> MyProfile()
        {
            var currentUser = await this.userManager.Users
    .Include(u => u.Posts) 
    .FirstOrDefaultAsync(u => u.Id == this.userManager.GetUserId(User));

            if (currentUser == null)
            {
                return this.Redirect("/Identity/Account/Login");
            }

            Console.WriteLine(currentUser.Posts.Count);

            var followerCount = this.dbContext.UserFollowers
        .Where(f => f.UserId == currentUser.Id && f.UnfollowedDate == null)
        .Count();

            var followingCount = this.dbContext.UserFollowers
                .Where(f => f.FollowerId == currentUser.Id && f.UnfollowedDate == null)
                .Count();

            var userProfileViewModel = new UserProfileViewModel
            {
                Username = currentUser.UserName,
                FirstName = currentUser.FirstName,
                LastName = currentUser.LastName,
                ProfilePictureUrl = currentUser.ProfilePictureURL,
                Gender = currentUser.Gender,
                BirthDay = currentUser.BirthDay,
                Description = currentUser.Description,
                City = currentUser.City,
                Country = currentUser.Country,
                Posts = currentUser.Posts.Select(post => new PostViewModel
                {
                    Id = post.Id,
                    Content = post.Content,
                    ImageUrl = post.ImageURL,
                    CreatedOn = post.CreatedOn.ToString("yyyy-MM-dd HH:mm"),
                    LikeCount = post.LikeCount,
                }).ToList(),
                FollowerCount = followerCount,
                FollowingCount = followingCount,
                ProfileUserId = currentUser.Id,
            };

            return View(userProfileViewModel);
        }

        [HttpGet]
        public IActionResult Search()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Redirect("/Identity/Account/Login");
            }

            return View();
        }

        public async Task<IActionResult> Messages(int? conversationId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Redirect("/Identity/Account/Login");
            }

            string profilePictureUrl = string.IsNullOrEmpty(currentUser.ProfilePictureURL)
        ? "/images/default-avatar.jpg" 
        : currentUser.ProfilePictureURL;

            var mutualFollowers = await dbContext.UserFollowers
                .Where(f => f.FollowerId == currentUser.Id && f.UnfollowedDate == null)
                .Select(f => f.UserId)
                .Intersect(
                    dbContext.UserFollowers
                        .Where(f => f.UserId == currentUser.Id && f.UnfollowedDate == null)
                        .Select(f => f.FollowerId)
                )
                .ToListAsync();

            var conversationUserIds = await dbContext.Conversations
                .Where(c => c.User1Id == currentUser.Id || c.User2Id == currentUser.Id)
                .Select(c => c.User1Id == currentUser.Id ? c.User2Id : c.User1Id)
                .ToListAsync();

            var newConversations = new List<Conversation>();

            foreach (var followerId in mutualFollowers)
            {
                if (!conversationUserIds.Contains(followerId)) 
                {
                    newConversations.Add(new Conversation
                    {
                        User1Id = currentUser.Id,
                        User2Id = followerId,
                        CreatedOn = DateTime.UtcNow
                    });
                }
            }

            if (newConversations.Any())
            {
                dbContext.Conversations.AddRange(newConversations);
                await dbContext.SaveChangesAsync();
            }

            var conversations = await dbContext.Conversations
                .Where(c => (c.User1Id == currentUser.Id && mutualFollowers.Contains(c.User2Id)) ||
                            (c.User2Id == currentUser.Id && mutualFollowers.Contains(c.User1Id)))
                .Select(c => new ConversationViewModel
                {
                    Id = c.Id,
                    OtherUserId = c.User1Id == currentUser.Id ? c.User2Id : c.User1Id,
                    OtherUserName = c.User1Id == currentUser.Id ? c.User2.UserName : c.User1.UserName,
                    OtherUserProfilePicture = c.User1Id == currentUser.Id ? c.User2.ProfilePictureURL : c.User1.ProfilePictureURL,
                })
                .ToListAsync();

            var followedUsers = await dbContext.Users
                .Where(u => mutualFollowers.Contains(u.Id) &&
                            !dbContext.Conversations.Any(c =>
                                (c.User1Id == currentUser.Id && c.User2Id == u.Id) ||
                                (c.User2Id == currentUser.Id && c.User1Id == u.Id)))
                .Select(u => new UserViewModel
                {
                    Id = u.Id,
                    Username = u.UserName,
                    ProfilePictureUrl = u.ProfilePictureURL,
                    IsFollowing = true
                })
                .ToListAsync();

            List<MessageViewModel> messages = new();
            if (conversationId.HasValue && conversations.Any(c => c.Id == conversationId.Value))
            {
                messages = await dbContext.Messages
                    .Where(m => m.ConversationId == conversationId.Value)
                    .OrderBy(m => m.SentOn)
                    .Select(m => new MessageViewModel
                    {
                        ConversationId = m.ConversationId,
                        SenderId = m.SenderId,
                        SenderName = m.Sender.UserName,
                        Content = m.Content,
                        SentOn = m.SentOn,
                    })
                    .ToListAsync();
            }

            var viewModel = new MessagesViewModel
            {
                CurrentUserId = currentUser.Id,
                CurrentUserName = currentUser.UserName,
                CurrentUserProfilePicture = profilePictureUrl,
                Conversations = conversations,
                FollowedUsers = followedUsers,
                SelectedConversationId = conversationId ?? 0,
                Messages = messages,
            };

            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return this.View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return this.View(
                new ErrorViewModel { RequestId = Activity.Current?.Id ?? this.HttpContext.TraceIdentifier });
        }
    }
}
