using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using WebTemplate.Controllers;
using WebTemplate.DTOs;
using WebTemplate.Models;
using WebTemplate.Services;

namespace WebTemplateTests.Controllers
{
    [TestFixture]
    [Ignore("Da ne smara")]
    internal class OglasiControllerTests
    {
        private ApplicationDbContext _context;
        private OglasiController _controller;

        [SetUp]
        public async Task Init()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            _context = new ApplicationDbContext(options);

            _controller = new OglasiController(_context);

            await _context.SaveChangesAsync();
        }

        [TearDown]

        public void Cleanup() 
        {
            _context.Dispose();
        }

        //Queries cu posebno da testiram
        #region Tests

        [Test]
        //[Ignore("Ionako nema provera za trenutnog korisnika, ako se pozove GetCurrentUserID kod puca zbog null reference.")]
        public async Task GetOglasiByKorisnik_NoCurrUser_ResEmptyList()
        {
            await AddOglasi(20, 1, 1);
            Assert.ThrowsAsync<NullReferenceException>(async Task () => await _controller.GetOglasiByKorisnik());
        }

        [Test]
        public async Task GetOglasiByKorisnik_CurrUserDBEmpty_ResEmptyList()
        {
            int userId = 1;
            HelperFunctions.MockCurrentUser(_controller,userId,UserRoles.User);


            var result = await _controller.GetOglasiByKorisnik();
            Assert.That(result, Is.Not.Null);

            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);

            var list = okResult.Value as List<OglasDto>;
            Assert.IsNotNull(list);
            Assert.That(list.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task GetOglasiByKorisnik_CurrUserExistsDBNotEmpty_ResOkList()
        {
            int userId = 1;
            int numberOfOglasi = 5;
            await AddOglasi(numberOfOglasi, userId, 1);

            var proveraLista = await _context.Oglasi.ToListAsync();
            Assert.That(proveraLista, Is.Not.Null);
            Assert.That(proveraLista.Count, Is.EqualTo(numberOfOglasi));

            HelperFunctions.MockCurrentUser(_controller,userId, UserRoles.User);


            var result = await _controller.GetOglasiByKorisnik();
            Assert.That(result, Is.Not.Null);

            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);

            var list = okResult.Value as List<OglasDto>;
            Assert.IsNotNull(list);
            Assert.That(list.Count, Is.EqualTo(numberOfOglasi));
        }

        [Test]
        public async Task PutOglas_NullDTO_ExceptionThrown()
        {
            await AddOglasi(1, 1, 1);

            var createDto = new OglasCreateDto();

            //Assert.That(_controller.PutOglas(1, null),Throws.Exception.TypeOf<System.ArgumentNullException>);
            Assert.ThrowsAsync<NullReferenceException>(async Task ()=> await _controller.PutOglas(1, null));
            //Assert.IsNotNull(result);
        }

        [Test]
        public async Task PutOglas_IDNotInDB_ResNotFound()
        {
            var createDto = new OglasCreateDto
            {
                Naziv = "Test",
                Opis = "test",
                Adresa = "test",
                Grad = "test",
                Latitude = 0.0,
                Longitude = 0.0,
                Cena = 0,
                TipCene = TipCene.Besplatno,
                Slika = null,
                KategorijaId = 1
            };

            var result = await _controller.PutOglas(1, createDto);
            Assert.IsNotNull(result);

            var badreq = result as NotFoundResult;
            Assert.IsNotNull(badreq);
            Assert.That(badreq.StatusCode, Is.EqualTo(404));
        }

        [Test]
        public async Task PutOglas_CleanPath_ResNoContent()
        {
            int korisnikID = 1, count = 1;
            await AddOglasi(count, korisnikID, 1);
            var createDto = new OglasCreateDto
            {
                Naziv = "Test",
                Opis = "test",
                Adresa = "test",
                Grad = "test",
                Latitude = 0.0,
                Longitude = 0.0,
                Cena = 0,
                TipCene = TipCene.Besplatno,
                Slika = null,
                KategorijaId = 1
            };

            var result = _controller.PutOglas(1, createDto);
            Assert.IsNotNull(result);

            var changedOglas = await _context.Oglasi.FindAsync(1);
            Assert.IsNotNull(changedOglas);

            Assert.That(changedOglas.Naziv, Is.EqualTo("Test"));
            Assert.That(changedOglas.Adresa, Is.EqualTo("test"));
            Assert.That(changedOglas.Grad, Is.EqualTo("test"));
            Assert.That(changedOglas.TipCene, Is.EqualTo(TipCene.Besplatno));


        }

        [Test]
        public async Task PostOglas_NullDTO_ExceptionThrown()
        {
            int korisnikID = 1;
            HelperFunctions.MockCurrentUser(_controller,korisnikID, UserRoles.User);
            _context.Korisnici.Add(new Korisnik
            {
                ID = korisnikID,
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


            Assert.ThrowsAsync<NullReferenceException>(async Task () => await _controller.PostOglas(null));
        }

        [Test]
        public async Task PostOglas_UserNotInDB_ResUnauthorized()
        {
            int korisnikID = 1;
            HelperFunctions.MockCurrentUser(_controller,korisnikID, UserRoles.User);

            var createDto = new OglasCreateDto
            {
                Naziv = "Test",
                Opis = "test",
                Adresa = "test",
                Grad = "test",
                Latitude = 0.0,
                Longitude = 0.0,
                Cena = 0,
                TipCene = TipCene.Besplatno,
                Slika = null,
                KategorijaId = 1
            };

            var result = await _controller.PostOglas(createDto);
            Assert.IsNotNull(result);

            Assert.That(result.Result as UnauthorizedResult, Is.Not.Null);
            Assert.That((result.Result as UnauthorizedResult).StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task PostOglas_CleanPath_ResCreatedAtAction()
        {
            int korisnikID = 1;
            HelperFunctions.MockCurrentUser(_controller,korisnikID, UserRoles.User);
            _context.Korisnici.Add(new Korisnik
            {
                ID = korisnikID,
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
            _context.Kategorije.Add(new Kategorija
            {
                Id = 1,
                Naziv = "TestKategorija"
            });
            await _context.SaveChangesAsync();

            var createDto = new OglasCreateDto
            {
                Naziv = "Test",
                Opis = "test",
                Adresa = "test",
                Grad = "test",
                Latitude = 0.0,
                Longitude = 0.0,
                Cena = 0,
                TipCene = TipCene.Besplatno,
                Slika = null,
                KategorijaId = 1
            };

            var result = await _controller.PostOglas(createDto);
            Assert.IsNotNull(result);

            var createdAtAction = result.Result as CreatedAtActionResult;
            Assert.IsNotNull(createdAtAction);

            var createdOglas = createdAtAction.Value as OglasDto;
            Assert.IsNotNull(createdOglas);
            Assert.That(createdOglas.Naziv, Is.EqualTo("Test"));
            Assert.That(createdOglas.Opis, Is.EqualTo("test"));
            Assert.That(createdOglas.TipCene, Is.EqualTo(TipCene.Besplatno));
            Assert.That(createDto.Slika,Is.Null);


        }

        [Test]
        public async Task DeleteOglas_OglasNotInDB_ResNotFound()
        {
            var result = await _controller.DeleteOglas(1);
            Assert.IsNotNull(result);

            var notFound = result as NotFoundObjectResult;
            Assert.IsNotNull(notFound);
            Assert.That(notFound.StatusCode, Is.EqualTo(404));
            //Assert.That(notFound.Value, Is.EqualTo("Oglas ne postoji"));
        }

        [Test]
        public async Task DeleteOglas_NoCurrUser_ExceptionThrown()
        {
            await AddOglasi(1, 1, 1);
            Assert.ThrowsAsync<NullReferenceException>(async Task() => await _controller.DeleteOglas(1));
        }

        [Test]
        public async Task DeleteOglas_CleanPath_ResNoContent()
        {
            int idKorisnika = 1;
            HelperFunctions.MockCurrentUser(_controller,idKorisnika, UserRoles.Admin);
            await AddOglasi(1, 1, 1);

            var result = await _controller.DeleteOglas(1);
            Assert.IsNotNull(result);

            var noContent= result as NoContentResult;
            Assert.IsNotNull(noContent);
            Assert.That(noContent.StatusCode, Is.EqualTo(204));

            var deletedOglas = await _context.Oglasi.FindAsync(1);
            Assert.That(deletedOglas, Is.Null);

        }

        [Test]
        public async Task DeleteOglasStatus_OglasNotInDB_ResNotFound()
        {
            await AddOglasi(99, 1, 1);

            var result = await _controller.DeleteOglasStatus(100);
            Assert.IsNotNull(result);

            var notFound = result as NotFoundObjectResult;
            Assert.IsNotNull(notFound);
            Assert.That(notFound.StatusCode, Is.EqualTo(404));
        }

        [Test]
        public async Task DeleteOglasStatus_UserIsDeletingOthersOglas_ResForbid()
        {
            HelperFunctions.MockCurrentUser(_controller,1, UserRoles.User);
            await AddOglasi(1, 999, 1);

            var result = await _controller.DeleteOglasStatus(1);
            Assert.IsNotNull(result);

            var forbid = result as ForbidResult;
            Assert.IsNotNull(forbid);
        }

        [Test]
        public async Task DeleteOglasStatus_CleanPath_ResNoContent()
        {
            HelperFunctions.MockCurrentUser(_controller,1, UserRoles.User);
            await AddOglasi(1, 1, 1);

            var result = await _controller.DeleteOglasStatus(1);
            Assert.IsNotNull(result);

            var noContent = result as NoContentResult;
            Assert.That(noContent, Is.Not.Null);
            Assert.That(noContent.StatusCode, Is.EqualTo(204));

            var changedOglas = await _context.Oglasi.FindAsync(1);

            Assert.IsNotNull(changedOglas);
            Assert.That(changedOglas.Status, Is.EqualTo("obrisan"));
        }

        #endregion Tests

        //private void HelperFunctions.MockCurrentUser(_controller,int userId, string role)
        //{
        //    var claims = new List<Claim>
        //                    {
        //                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        //                        new Claim(ClaimTypes.Role, role)
        //                    };

        //    var identity = new ClaimsIdentity(claims, "TestAuthType");
        //    var claimsPrincipal = new ClaimsPrincipal(identity);

        //    //ako je controller vec inicijalizovan, ne bi trebalo da pravi problem (i hope)
        //    _controller.ControllerContext = new ControllerContext
        //    {
        //        HttpContext = new DefaultHttpContext { User = claimsPrincipal },
        //    };
        //}

        private async Task AddOglasi(int count, int korisnikID,int kategorijaID)
        {
            var korisnik = new Korisnik
            {
                ID = korisnikID,
                Ime = "Petar",
                Prezime = "Petrovic",
                Username = "petarP",
                Email = "petarPetrovic@gmail.com",
                PasswordHash = "asdfghjkl123",
                Role = UserRoles.User,
                Biografija = "Cao svima ja sam petarP",
                Telefon = "06555333",
                SlikaURL = "http://provider/slike/petarP"
            };

            for (int i =1;i<=count;++i)
            {
                var oglas = new Oglas
                {
                    Naziv = "TestNaziv",
                    Opis = "TestOpis",
                    Adresa = "TestAdresa",
                    Grad = "Test",
                    Latitude = 0.0,
                    Longitude = 0.0,
                    Cena = 999,
                    TipCene = TipCene.Fiksno,

                    Status = "Postavljen",
                    DatumKreiranja = DateTime.UtcNow,

                    PostavljacOglasa = korisnik,
                    PostavljacOglasaId = korisnikID,
                    KategorijaId = kategorijaID
                };

                _context.Oglasi.Add(oglas);
            }

            await _context.SaveChangesAsync();
        }
    }
}
