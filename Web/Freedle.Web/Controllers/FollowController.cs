using Freedle.Data.Models;
using Freedle.Data;
using Freedle.Web.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using System;
using System.Linq;
using Freedle.Web.ViewModels;

namespace Freedle.Web.Controllers
{
    public class FollowController : BaseController
    {
        private readonly ApplicationDbContext dbContext;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IHubContext<ChatHub> hubContext;

        public FollowController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, IHubContext<ChatHub> hubContext)
        {
            this.dbContext = dbContext;
            this.userManager = userManager;
            this.hubContext = hubContext;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFollow(string id)
        {
            Console.WriteLine("Received ID: " + id);

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null || id == currentUserId)
            {
                return BadRequest();
            }

            var userToFollow = await dbContext.Users
                .Include(u => u.Followers)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (userToFollow == null)
            {
                return NotFound();
            }

            var existingFollow = await dbContext.UserFollowers
                .FirstOrDefaultAsync(f => f.FollowerId == currentUserId && f.UserId == id);

            if (existingFollow != null)
            {
                dbContext.UserFollowers.Remove(existingFollow); // Ако вече го следва -> Unfollow
            }
            else
            {
                // Ако не го следва - Follow

                dbContext.UserFollowers.Add(new UserFollower
                {
                    FollowerId = currentUserId,
                    UserId = id,
                });
            }

            await dbContext.SaveChangesAsync();
            return Redirect(Request.Headers["Referer"].ToString());
        }

        [HttpGet]
        public IActionResult FollowingList(string id)
        {
            var following = dbContext.UserFollowers
                .Where(f => f.FollowerId == id)
                .Select(f => new UserViewModel
                {
                    Id = f.UserId,
                    Username = f.User.UserName,
                    ProfilePictureUrl = f.User.ProfilePictureURL,
                })
                .ToList();

            return View(following);
        }

        [HttpGet]
        public async Task<IActionResult> FollowersList(string id)
        {
            var user = await dbContext.Users
                .Include(u => u.Followers)
                .ThenInclude(f => f.Follower)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            var followers = user.Followers
                .Select(f => new UserViewModel
                {
                    Id = f.Follower.Id,
                    Username = f.Follower.UserName,
                    ProfilePictureUrl = f.Follower.ProfilePictureURL,
                })
                .ToList();

            return View(followers);
        }

        [HttpGet]
        public async Task<IActionResult> UserFollowingList(string id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var following = await dbContext.UserFollowers
                .Where(f => f.FollowerId == id)
                .Include(f => f.User) // Включваме User, за да имаме данни за него
                .Select(f => new UserViewModel
                {
                    Id = f.User.Id,
                    Username = f.User.UserName,
                    ProfilePictureUrl = f.User.ProfilePictureURL,
                    IsFollowing = dbContext.UserFollowers.Any(uf => uf.FollowerId == currentUserId && uf.UserId == f.User.Id)
                })
                .ToListAsync();

            return View(following);
        }

        [HttpGet]
        public async Task<IActionResult> UserFollowersList(string id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var followers = await dbContext.UserFollowers
                .Where(f => f.UserId == id)
                .Include(f => f.Follower)
                .Select(f => new UserViewModel
                {
                    Id = f.Follower.Id,
                    Username = f.Follower.UserName,
                    ProfilePictureUrl = f.Follower.ProfilePictureURL,
                    IsFollowing = dbContext.UserFollowers.Any(uf => uf.FollowerId == currentUserId && uf.UserId == f.Follower.Id) // Проверяваме дали логнатият потребител следва този последовател
                })
                .ToListAsync();

            return View(followers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unfollow(string userId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            if (string.IsNullOrEmpty(userId)) return BadRequest("Invalid user ID");

            var followerEntry = await dbContext.UserFollowers
                .FirstOrDefaultAsync(f => f.UserId == userId && f.FollowerId == currentUser.Id);

            if (followerEntry != null)
            {
                dbContext.UserFollowers.Remove(followerEntry);
                await dbContext.SaveChangesAsync();
            }

            return Redirect(Request.Headers["Referer"].ToString());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFollower(string userId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var followerToRemove = await dbContext.UserFollowers
                .FirstOrDefaultAsync(uf => uf.UserId == currentUser.Id && uf.FollowerId == userId);

            if (followerToRemove == null) return NotFound();

            dbContext.UserFollowers.Remove(followerToRemove);
            await dbContext.SaveChangesAsync();

            return RedirectToAction("FollowersList", new { id = currentUser.Id });
        }
    }
}
