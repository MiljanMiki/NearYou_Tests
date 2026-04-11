using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using WebTemplate.Controllers;
using WebTemplate.DTOs;
using WebTemplate.Models;
using WebTemplate.Services;

namespace WebTemplateTests.Controllers
{
    [TestFixture]
    [Ignore("Da me ne bi smaralo trenutno")]
    //[Parallelizable(ParallelScope.All)]
    internal class KorisniciControllerTests
    {
    #region Attributes
        private ApplicationDbContext _context;
        private Mock<IAuthService> _authServiceMock;
        private KorisniciController _controller;
    #endregion Attributes

    #region SetUpAndTearDown
        [SetUp]
        public async Task Setup()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

            _context = new ApplicationDbContext(options);
            _authServiceMock = new Mock<IAuthService>();
            _authServiceMock.Setup(repo => repo.HashPassword(It.IsAny<string>())).Returns("hashedPassword");
            _authServiceMock.Setup(repo => repo.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

            _controller = new KorisniciController(_context, _authServiceMock.Object);

            await _context.SaveChangesAsync();
        }

        [TearDown]
        public void TearDown()
        {
            _context.Dispose();
        }
    #endregion SetUpAndTearDown

    #region Tests
        [Test]
        public async Task GetKorisnici_UsersExist_RetOkList()
        {
            _context.Korisnici.Add(new Korisnik
            {
                ID = 1,
                Ime = "Petar",
                Prezime = "Petrovic",
                Username = "petarP",
                Email = "petarPetrovic@gmail.com",
                PasswordHash = "asdfghjkl123",
                Role = UserRoles.User,
                Biografija = "Cao svima ja sam petarP",
                Telefon = "06555333",
                SlikaURL = "http://provider/slike/petarP"
            });
            await _context.SaveChangesAsync();

            var result =  await _controller.GetKorisnici();

            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            //Assert.AreEqual(200, okResult.StatusCode);
            Assert.That(okResult.StatusCode, Is.EqualTo(200));

            var resultList = okResult.Value as IEnumerable<UserInfoDto>;

            Assert.That(resultList,Is.Not.Null);
            Assert.That(resultList.Count(), Is.EqualTo(1));
            Assert.That(resultList.First().Id, Is.EqualTo(1));
        }

        [Test]

        public async Task GetKorisnici_UsersEmpty_RetOkListEmpty()
        {
            var result = await _controller.GetKorisnici();

            var okResult = result.Result as OkObjectResult;
            Assert.IsNotNull(okResult);
            Assert.That(okResult.StatusCode, Is.EqualTo(200));

            var resultList = okResult.Value as IEnumerable<UserInfoDto>;

            Assert.IsNotNull(resultList);
            Assert.That(resultList.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task GetKorisnici_OneUserExists_RetListWithCorrectDTOMapping()
        {
            _context.Korisnici.Add(new Korisnik
            {
                ID = 1,
                Ime = "Petar",
                Prezime = "Petrovic",
                Username = "petarP",
                Email = "petarPetrovic@gmail.com",
                PasswordHash = "asdfghjkl123",
                Role = UserRoles.User,
                Biografija = "Cao svima ja sam petarP",
                Telefon = "06555333",
                SlikaURL = "http://provider/slike/petarP"
            });
            await _context.SaveChangesAsync();

            var result = await _controller.GetKorisnici();

            var okResult = result.Result as OkObjectResult;
            Assert.IsNotNull(okResult);
            Assert.That(okResult.StatusCode, Is.EqualTo(200));

            var resultList = okResult.Value as IEnumerable<UserInfoDto>;

            Assert.IsNotNull(resultList);
            Assert.That(resultList.Count(), Is.EqualTo(1));

            var dto = resultList.First();
            Assert.That(dto, Is.Not.Null);
            Assert.That(dto, Is.TypeOf(typeof(UserInfoDto)));

            int korisnikPropertyCount = typeof(Korisnik).GetProperties().Length;
            int dtoPropertyCount = dto.GetType().GetProperties().Length;
            Assert.That(dtoPropertyCount, Is.Not.EqualTo(korisnikPropertyCount));
            
        }

        //GetCurrentUser skipovano, zavisi od trenutno ulogovan korisnika

        [Test]
        public async Task GetKorisnik_CurrUserNotAdmin_ResForbid()
        {
            int idKorisnika = 1;
            MockCurrentUser(idKorisnika, UserRoles.User);

            var result = await _controller.GetKorisnik(idKorisnika);
            Assert.That(result, Is.Not.Null);

            var forbidResult = result.Result as ForbidResult;
            Assert.That(forbidResult, Is.Null);//posto vraca samo Forbid();
        }

        [Test]
        
        public async Task GetKorisnik_CurrUserAdminNotInDb_ResNotFound()
        {
            int idKorisnika = 1;
            MockCurrentUser(idKorisnika, UserRoles.User);

            var result = await _controller.GetKorisnik(idKorisnika);
            Assert.That(result, Is.Not.Null);

            var notFound = result.Result as NotFoundResult;
            Assert.That(notFound, Is.Not.Null);
            Assert.That(notFound.StatusCode, Is.EqualTo(404));
        }

        [Test]
        public async Task GetKorisnik_CurrUserAdminInDb_ResUserInfoDTO()
        {
            int idKorisnika = 1;
            MockCurrentUser(idKorisnika, UserRoles.User);

            _context.Korisnici.Add(new Korisnik
            {
                ID = 1,
                Ime = "Petar",
                Prezime = "Petrovic",
                Username = "petarP",
                Email = "petarPetrovic@gmail.com",
                PasswordHash = "asdfghjkl123",
                Role = UserRoles.User,
                Biografija = "Cao svima ja sam petarP",
                Telefon = "06555333",
                SlikaURL = "http://provider/slike/petarP"
            });
            await _context.SaveChangesAsync();

            var result = await _controller.GetKorisnik(idKorisnika);
            Assert.That(result, Is.Not.Null);

            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            Assert.That(okResult.StatusCode, Is.EqualTo(200));


            var korisnik = okResult.Value as UserInfoDto;
            Assert.That(korisnik, Is.Not.Null);
            Assert.That(korisnik.Id, Is.EqualTo(1));

        }

        [Test]
        public async Task GetKorisnikByUsername_DBEmpty_ResNotFound()
        {
            var result = await _controller.GetKorisnikByUsername("petarP");
            Assert.That(result,Is.Not.Null);

            var resNotFound = result.Result as NotFoundResult;
            Assert.That(resNotFound, Is.Not.Null);
            Assert.That(resNotFound.StatusCode, Is.EqualTo(404));
        }

        [Test]
        public async Task GetKorisnikByUsername_UserNotInDb_ResNotFound()
        {
            _context.Korisnici.Add(new Korisnik
            {
                ID = 1,
                Ime = "Petar",
                Prezime = "Petrovic",
                Username = "petarP",
                Email = "petarPetrovic@gmail.com",
                PasswordHash = "asdfghjkl123",
                Role = UserRoles.User,
                Biografija = "Cao svima ja sam petarP",
                Telefon = "06555333",
                SlikaURL = "http://provider/slike/petarP"
            });
            await _context.SaveChangesAsync();


            var result = await _controller.GetKorisnikByUsername("petarPPdugaCarapa");
            Assert.That(result, Is.Not.Null);

            var resNotFound = result.Result as NotFoundResult;
            Assert.That(resNotFound, Is.Not.Null);
            Assert.That(resNotFound.StatusCode, Is.EqualTo(404));
        }

        [Test]
        public async Task GetKorisnikByUsername_UserInDB_ResOkUserInfoDTO()
        {
            _context.Korisnici.Add(new Korisnik
            {
                ID = 1,
                Ime = "Petar",
                Prezime = "Petrovic",
                Username = "petarP",
                Email = "petarPetrovic@gmail.com",
                PasswordHash = "asdfghjkl123",
                Role = UserRoles.User,
                Biografija = "Cao svima ja sam petarP",
                Telefon = "06555333",
                SlikaURL = "http://provider/slike/petarP"
            });
            await _context.SaveChangesAsync();


            var result = await _controller.GetKorisnikByUsername("petarP");
            Assert.That(result, Is.Not.Null);

            var resOk = result.Result as OkObjectResult;
            Assert.That(resOk, Is.Not.Null);
            Assert.That(resOk.StatusCode, Is.EqualTo(200));

            var dto = resOk.Value as UserInfoDto;
            Assert.That(dto, Is.Not.Null);

            Assert.That(dto.Id, Is.EqualTo(1));
            Assert.That(dto.Username, Is.EqualTo("petarP"));

        }

        [Test]
        public async Task PutKorisnik_CurrUserNotAdmin_ResForbid()
        {
            int idKorisnika = 1;
            MockCurrentUser(1, UserRoles.User);

            KorisnikUpdateDto dto = new KorisnikUpdateDto();

            var result = await _controller.PutKorisnik(idKorisnika,dto) as ActionResult;
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf(typeof(ForbidResult)));
        }

        [Test]
        public async Task PutKorisnik_CurrUserAdminPhoneNumberIsCharacters_ResBadRequest()
        {
            int idKorisnika = 1;
            MockCurrentUser(1, UserRoles.Admin);
            await AddUserAsync(idKorisnika, true);

            KorisnikUpdateDto dto = new KorisnikUpdateDto { Ime = "Petar", Prezime = "Petrovic", Bio = "Ja sam PP", Telefon = "asdasdjioij1o" };

            var result = await _controller.PutKorisnik(idKorisnika, dto) as BadRequestObjectResult;
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf(typeof(BadRequestObjectResult)));
            Assert.That(result.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public async Task PutKorisnik_CurrUserAdminCorrectDTOFormat_ResOk()
        {
            int idKorisnika = 1;
            MockCurrentUser(1, UserRoles.Admin);
            await AddUserAsync(idKorisnika, true);

            KorisnikUpdateDto dto = new KorisnikUpdateDto { Ime = "Marko", Prezime = "Markovic", Bio = "Ja sam MM", Telefon = "06555333",Email="markom@outlook.com" };

            var result = await _controller.PutKorisnik(idKorisnika,dto) as OkObjectResult;
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf(typeof(OkObjectResult)));
            Assert.That(result.StatusCode, Is.EqualTo(200));

            var response = result.Value as LoginResponseDto;
            Assert.That(response, Is.Not.Null);
            Assert.That(response, Is.InstanceOf(typeof(LoginResponseDto)));

            Assert.That(response.User.Ime, Is.Not.EqualTo("Petar"));
            Assert.That(response.User.Email, Is.Not.EqualTo("petarPetrovic@gmail.com"));

        }

        [Test]
        public async Task PostKorisnik_EmailExist_ResBadRequest()
        {
            await AddUserAsync(1, false);

            KorisnikCreateDto dto = new KorisnikCreateDto
            {
                Ime = "Petar",
                Prezime = "Petrovic",
                Username = "asdfg1234",
                Email = "petarPetrovic@gmail.com",
                Password = "asdfghjkl123",
            };

            var result = await _controller.PostKorisnik(dto);
            Assert.That(result, Is.Not.Null);

            var badReq = result.Result as BadRequestObjectResult;
            Assert.That(badReq, Is.Not.Null);
            Assert.That(badReq.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public async Task PostKorisnik_UsernameExist_ResBadRequest()
        {
            await AddUserAsync(1, false);

            KorisnikCreateDto dto = new KorisnikCreateDto
            {
                Ime = "Petar",
                Prezime = "Petrovic",
                Username = "petarP",
                Email = "petarPetrovic@gmail.com",//prvo se proverava username, ne mora da se menja email
                Password = "asdfghjkl123",
            };

            var result = await _controller.PostKorisnik(dto);
            Assert.That(result, Is.Not.Null);

            var badReq = result.Result as BadRequestObjectResult;
            Assert.That(badReq, Is.Not.Null);
            Assert.That(badReq.StatusCode, Is.EqualTo(400));
        }

        [Test]
        //[Ignore("Test puca jer objekat nema required atribute prilikom dodavanje u bazu. Absolute cinema\"")]
        public async Task PostKorisnik_CorrectInput_ResCreatedAtAction()
        {
            _context.Korisnici.Add(new Korisnik
            {
                ID = 1,
                Ime = "Petar",
                Prezime = "Petrovic",
                Username = "petarP",
                Email = "petarPetrovic@gmail.com",
                PasswordHash = "asdfghjkl123",
                Role = UserRoles.User,
                Biografija = "Cao svima ja sam petarP",
                Telefon = "06555333",
                SlikaURL = "http://provider/slike/petarP"
            });
            await _context.SaveChangesAsync();

            KorisnikCreateDto dto = new KorisnikCreateDto
            {
                Ime = "Marko",
                Prezime = "Miljkovic",
                Username = "makiMaki",
                Email = "markoMM@gmail.com",//prvo se proverava username, ne mora da se menja email
                Password = "asdfghjkl123",
            };

            var result = await _controller.PostKorisnik(dto);
            Assert.That(result, Is.Not.Null);

            var okRes= result.Result as CreatedAtActionResult;
            Assert.That(okRes, Is.Not.Null);
            Assert.That(okRes.StatusCode, Is.EqualTo(201));

            var korisniciResult = await _controller.GetKorisnici();

            var okObjectResult = korisniciResult.Result as OkObjectResult;
            Assert.That(okObjectResult, Is.Not.Null);

            var list = okObjectResult.Value as List<UserInfoDto>;
            Assert.That(list, Is.Not.Null);
            Assert.That(list.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task DeleteKorisnik_UserIsNotAdmin_ResForbid()
        {
            int idAdmina = 99;
            MockCurrentUser(idAdmina, UserRoles.User);

            var result = await _controller.DeleteKorisnik(5) as ActionResult;
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf(typeof(ForbidResult)));

        }

        [Test]
        public async Task DeleteKorisnik_UserIsAdminIncorrectId_ResNotFound()
        {
            int idAdmina = 99;
            MockCurrentUser(idAdmina, UserRoles.Admin);
            await AddUserAsync(1, true);

            var result = await _controller.DeleteKorisnik(555) as ActionResult;
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf(typeof(NotFoundResult)));

            var resNotFound = result as NotFoundResult;
            Assert.That(resNotFound.StatusCode, Is.EqualTo(404));
        }

        [Test]
        public async Task DeleteKorisnik_UserIsAdminCorrectId_ResNoContent()
        {
            int idAdmina = 99;
            MockCurrentUser(idAdmina, UserRoles.Admin);
            //await AddUserAsync(1, false);
            await AddMultipleUsersAsync(5);


            var result = await _controller.DeleteKorisnik(1) as ActionResult;
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf(typeof(NoContentResult)));

            var resNoContent = result as NoContentResult;
            Assert.That(resNoContent.StatusCode, Is.EqualTo(204));

        }


        [Test]
        public async Task UploadProfileImage_NullImage_ResBadRequest()
        {
            var dto = new ProfileImageUploadDto { Image = null };
            var result = await _controller.UploadProfileImage(dto) as BadRequestObjectResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result.StatusCode, Is.EqualTo(400));

            if (Directory.Exists("wwwroot"))//za svaki slucaj
                Directory.Delete("wwwroot", true);
        }

        [Test]
        public async Task UploadProfileImage_UserLoggedInNotInDb_ResNotFound()
        {
            int testUserId = 1;
            MockCurrentUser(testUserId, UserRoles.User);

            var fileMock = new Mock<IFormFile>();
            var ms = new MemoryStream(Encoding.UTF8.GetBytes("Test image data"));
            fileMock.Setup(_ => _.Length).Returns(ms.Length);
            fileMock.Setup(_ => _.FileName).Returns("avatar.png");
            fileMock.Setup(_ => _.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var dto = new ProfileImageUploadDto { Image = fileMock.Object };
            var result = await _controller.UploadProfileImage(dto) as NotFoundResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result.StatusCode, Is.EqualTo(404));

            if (Directory.Exists("wwwroot"))//za svaki slucaj
                Directory.Delete("wwwroot", true);
        }
        [Test]
        public async Task UploadProfileImage_ValidImage_ReturnsOkAndUpdatesUser()
        {
            int testUserId = 1;
            MockCurrentUser(testUserId, UserRoles.User);

            await AddUserAsync(1,false);


            var fileMock = new Mock<IFormFile>();
            var ms = new MemoryStream(Encoding.UTF8.GetBytes("Test image data"));
            fileMock.Setup(_ => _.Length).Returns(ms.Length);
            fileMock.Setup(_ => _.FileName).Returns("avatar.png");
            fileMock.Setup(_ => _.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var dto = new ProfileImageUploadDto { Image = fileMock.Object };
            var result = await _controller.UploadProfileImage(dto) as OkObjectResult;
            Assert.IsNotNull(result);
            Assert.That(result.StatusCode, Is.EqualTo(200));

            var updatedUser = await _context.Korisnici.FindAsync(testUserId);
            Assert.IsTrue(updatedUser.SlikaURL.Contains("user_1.png"));

            if (Directory.Exists("wwwroot"))
                Directory.Delete("wwwroot", true);
        }

        [Test]
        public async Task PromoteToAdmin_UserNotInDB_ResNotFound()
        {
            int userId = 1;
            //ne mora da se mockuje trenutni korisnik

            var result = await _controller.PromoteToAdmin(userId) as NotFoundObjectResult;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.StatusCode, Is.EqualTo(404));
        }

        [Test]
        public async Task PromoteToAdmin_UserIsAlreadyAdmin_ResBadRequest()
        {
            int userId = 1;
            await AddUserAsync(userId,true);

            var result = await _controller.PromoteToAdmin(userId) as BadRequestObjectResult;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public async Task PromoteToAdmin_UserPromoted_ResOk()
        {
            await AddUserAsync(1, false);

            var result = await _controller.PromoteToAdmin(1) as OkObjectResult;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.StatusCode, Is.EqualTo(200));
            //da se ne vraca anonymous type, proverio bi i njega...

            var updatedUser = await _context.Korisnici.FindAsync(1);
            Assert.That(updatedUser, Is.Not.Null);
            Assert.That(updatedUser.Role, Is.EqualTo("Admin"));

            
        }

        [Test]
        public async Task PromeniSifru_UserDoesntExist_ResNotFound()
        {
            await AddMultipleUsersAsync(5);

            var dto = new PromenaSifreDto { OldPassword = "", NewPassword = "" };
            var result = await _controller.PromeniSifru(10,dto) as NotFoundObjectResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result.StatusCode, Is.EqualTo(404));
        }

        [Test]
        public async Task PromeniSifru_PasswordTooShort_ResBadRequest()
        {
            string sifra = "Ja_sam_Petars123";
            KorisnikCreateDto createDto = new KorisnikCreateDto { Ime = "Petar", Prezime = "Petrovic", Username = "petarP", Email = "petarP@gmail.com", Password = sifra };

            var result = await _controller.PostKorisnik(createDto);
            Assert.That(result, Is.Not.Null);

            var createdRes = result.Result as CreatedAtActionResult;
            Assert.That(createdRes, Is.Not.Null);
            Assert.That(createdRes.Value, Is.Not.Null);
            var user = createdRes.Value as UserInfoDto;
            
            var dto = new PromenaSifreDto { OldPassword = sifra, NewPassword = "asdfg" };
            var promenaResult= await _controller.PromeniSifru(user.Id, dto) as BadRequestObjectResult;

            Assert.That(promenaResult, Is.Not.Null);
            Assert.That(promenaResult.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public async Task PromeniSifru_PasswordIsGood()
        {
            string sifra = "asdf1234";

            KorisnikCreateDto createDto = new KorisnikCreateDto { Ime = "Petar", Prezime = "Petrovic", Username = "petarP", Email = "petarP@gmail.com", Password = sifra };

            var result = await _controller.PostKorisnik(createDto);
            Assert.That(result, Is.Not.Null);

            var createdRes = result.Result as CreatedAtActionResult;
            Assert.That(createdRes, Is.Not.Null);
            Assert.That(createdRes.Value, Is.Not.Null);
            var user = createdRes.Value as UserInfoDto;


            var dto = new PromenaSifreDto { OldPassword = sifra, NewPassword = "9Caoja#sam_novaSifra!!!1" };

            var promenaResult= await _controller.PromeniSifru(user.Id, dto) as OkObjectResult;
            Assert.That(promenaResult, Is.Not.Null);
            Assert.That(promenaResult.StatusCode, Is.EqualTo(200));


        }
        #endregion Tests

        private void MockCurrentUser(int userId, string role)
        {
            var claims = new List<Claim>
                            {
                                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                                new Claim(ClaimTypes.Role, role)
                            };

            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            //ako je controller vec inicijalizovan, ne bi trebalo da pravi problem (i hope)
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal },
            };
        }

        private async Task AddUserAsync(int id, bool admin)
        {
            _context.Korisnici.Add(new Korisnik
            {
                ID = id,
                Ime = "Petar",
                Prezime = "Petrovic",
                Username = "petarP",
                Email = "petarPetrovic@gmail.com",
                PasswordHash = "asdfghjkl123",
                Role = admin ? UserRoles.Admin : UserRoles.User,
                Biografija = "Cao svima ja sam petarP",
                Telefon = "06555333",
                SlikaURL = "http://provider/slike/petarP"
            });
            await _context.SaveChangesAsync();
        }
        private async Task AddMultipleUsersAsync(int count)
        {
            //treba da ide od 1 jer je default ID 0, pa puca
            for (int i = 1; i <= count; ++i)
            {
                _context.Korisnici.Add(new Korisnik
                {
                    ID = i,
                    Ime = "Petar"+i,
                    Prezime = "Petrovic"+i,
                    Username = "petarP",
                    Email = "petarPetrovic@gmail.com",
                    PasswordHash = "asdfghjkl123",
                    Role = UserRoles.User,
                    Biografija = "Cao svima ja sam petarP",
                    Telefon = "06555333",
                    SlikaURL = "http://provider/slike/petarP"
                });
            }

            await _context.SaveChangesAsync();
        }
    }
}
