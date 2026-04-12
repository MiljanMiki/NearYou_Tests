using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace WebTemplateTests
{
    //ovde bih mogdao i da dodam pomocne f-je za dodavanje test podataka
    static internal class HelperFunctions
    {
        public static void MockCurrentUser(ControllerBase controller,int userId, string role)
        {
            var claims = new List<Claim>
                            {
                                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                                new Claim(ClaimTypes.Role, role)
                            };

            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            //ako je controller vec inicijalizovan, ne bi trebalo da pravi problem (i hope)
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal },
            };
        }
    }
}
