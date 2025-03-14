using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Freedle.Web.ViewModels
{
    public class EditMyProfileViewModel
    {
        public string Username { get; set; } 

        public string Description { get; set; } 

        public IFormFile ProfilePicture { get; set; } 
    }
}
