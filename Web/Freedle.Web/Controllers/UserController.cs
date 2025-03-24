using Freedle.Data.Models;
using Freedle.Data;
using Freedle.Web.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System;
using Freedle.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace Freedle.Web.Controllers
{
    public class UserController : BaseController
    {
        private readonly ApplicationDbContext dbContext;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IHubContext<ChatHub> hubContext;

        public UserController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, IHubContext<ChatHub> hubContext)
        {
            this.dbContext = dbContext;
            this.userManager = userManager;
            this.hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> UserProfile(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var user = await dbContext.Users
                .Where(u => u.Id == id)
                .Select(u => new UserProfileViewModel
                {
                    ProfileUserId = u.Id,
                    Username = u.UserName,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Description = u.Description,
                    ProfilePictureUrl = u.ProfilePictureURL ?? "/images/default-avatar.png",
                    Gender = u.Gender,
                    FollowerCount = u.Followers.Count,
                    FollowingCount = u.Following.Count,
                    Posts = u.Posts.Select(p => new PostViewModel
                    {
                        Id = p.Id,
                        ImageUrl = p.ImageURL,
                    }).ToList(),
                    IsFollowing = u.Followers.Any(f => f.FollowerId == currentUserId),
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound();
            }


            Console.WriteLine("ProfileUserId: " + user.ProfileUserId);
            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var user = await this.userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var model = new EditMyProfileViewModel
            {
                Username = user.UserName,
                Description = user.Description
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> EditProfile(EditMyProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction("MyProfile", "Home");
            }

            var user = await this.dbContext.Users.FirstOrDefaultAsync(u => u.Id == this.userManager.GetUserId(User));
            if (user == null)
            {
                return NotFound();
            }

            user.UserName = model.Username;
            user.NormalizedUserName = model.Username.ToUpper();
            user.Description = model.Description;

            // Ако има нова снимка, качваме я и променяме URL
            if (model.ProfilePicture != null)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/profile_pics");

                // Проверка дали папката съществува, ако не – създаваме я
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.ProfilePicture.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ProfilePicture.CopyToAsync(fileStream);
                }

                user.ProfilePictureURL = "/images/profile_pics/" + uniqueFileName;
            }

            try
            {
                await this.dbContext.SaveChangesAsync();
                return RedirectToAction("MyProfile", "Home");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating profile: {ex.Message}");
                return StatusCode(500, new { message = "Error updating profile", error = ex.Message });
            }
        }
    }
}
