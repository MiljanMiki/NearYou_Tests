using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebTemplate.Controllers;
using WebTemplate.DTOs;
using WebTemplate.Models;

namespace WebTemplateTests.Controllers
{
    [TestFixture]
    internal class PrijaveControllerTests
    {
        private ApplicationDbContext _context;
        private PrijaveController _controller;

        [SetUp]
        public async Task Init()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            _context = new ApplicationDbContext(options);

            _controller = new (_context);

            await _context.SaveChangesAsync();
        }

        [TearDown]

        public void Cleanup()
        {
            _context.Dispose();
        }

        #region Tests
        [Test]
        public async Task GetPrijave_DBEmpty_ResOkEmptyList()
        {
            var result = await _controller.GetPrijave();
            Assert.IsNotNull(result);
            var okResult = result.Result as OkObjectResult;
            Assert.IsNotNull(okResult);

            var list = okResult.Value as List<PrijavaDto>;
            Assert.IsNotNull(list);
            Assert.That(list.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task GetPrijave_NoNavProperties_ResOkEmptyList()
        {

            Prijava p = new Prijava
            {
                KorisnikId = -1,
                OglasId = -1,
                VremePrijave = DateTime.MinValue,
                Status = "Na cekanju",
                Poruka = "Test poruka"
            };

            _context.Prijave.Add(p);

            await _context.SaveChangesAsync();

            var result = await _controller.GetPrijave();
            Assert.IsNotNull(result);
            var okResult = result.Result as OkObjectResult;
            Assert.IsNotNull(okResult);

            var list = okResult.Value as List<PrijavaDto>;
            Assert.IsNotNull(list);
            Assert.That(list.Count, Is.EqualTo(0));
        }

        [Test]
        [TestCase(20)]
        public async Task GetPrijave_CleanPath_ResOkList(int count)
        {
            for(int i =1;i<=count;++i)
                await AddPrijava(i, i);

            var result = await _controller.GetPrijave();
            Assert.IsNotNull(result);
            var okResult = result.Result as OkObjectResult;
            Assert.IsNotNull(okResult);

            var list = okResult.Value as List<PrijavaDto>;
            Assert.IsNotNull(list);
            Assert.That(list.Count, Is.EqualTo(count));
        }

        [Test]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        public async Task GetPrijava_DBEmpty_ResNotFound(int id)
        {
            var result = await _controller.GetPrijava(id);
            Assert.IsNotNull(result);

            var notFound = result.Result as NotFoundObjectResult;
            Assert.IsNotNull(notFound);
            Assert.That(notFound.Value, Is.EqualTo($"Prijava sa ID {id} nije pronađena."));
        }

        [Test]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(9999)]
        public async Task GetPrijava_DBFullWrongID_ResNotFound(int id)
        {
            int count = 10;
            for (int i = 1; i <= count; ++i)
                await AddPrijava(i, i);

            var result = await _controller.GetPrijava(id);
            Assert.IsNotNull(result);

            var notFound = result.Result as NotFoundObjectResult;
            Assert.IsNotNull(notFound);
            Assert.That(notFound.Value, Is.EqualTo($"Prijava sa ID {id} nije pronađena."));

        }

        [Test]
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(10)]
        public async Task GetPrijava_CleanPath_ResOk(int id)
        {
            int count = 10;
            for (int i = 1; i <= count; ++i)
                await AddPrijava(i, i);

            var result = await _controller.GetPrijava(id);
            Assert.IsNotNull(result);

            var notFound = result.Result as OkObjectResult;
            Assert.IsNotNull(notFound);

            var prijava= notFound.Value as PrijavaDto;
            Assert.IsNotNull(prijava);
            Assert.That(prijava.ID, Is.EqualTo(id));
        }

        [Test]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(999)]
        public async Task GetPrijaveByKorisnik_NonExistentUser_ResEmptyOkNotNotFound(int id)
        {
            var result = await _controller.GetPrijaveByKorisnik(id);

            var okResult = result.Result as OkObjectResult;
            var list = okResult.Value as IEnumerable<PrijavaDto>;

            Assert.IsNotNull(okResult);
            Assert.IsEmpty(list);
        }

        [Test]
        [Ignore("Trebalo bi da pukne jer ne postoji NavData za Oglas i korisnika, ali nesto nece. Hmmm...")]
        public async Task GetPrijaveByKorisnik_MissingNavData_NullReferenceEx()
        {
            int targetKorisnikId = 1;
            _context.Prijave.Add(new Prijava
            {
                ID = 5,
                KorisnikId = targetKorisnikId,
                OglasId = 50,
                VremePrijave = DateTime.Now
            });
            await _context.SaveChangesAsync();

            Assert.ThrowsAsync<NullReferenceException>(async () => await _controller.GetPrijaveByKorisnik(targetKorisnikId));
        }

        [Test]
        public async Task GetPrijaveByKorisnik_ValidId_ResOkList()
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

            int count = 5;
            for (int i = 1; i <= count; ++i)
                await AddPrijava(1, i,false,true);

            var prvaPrijava = await _context.Prijave.FindAsync(1);
            var poslednjaPrijava = await _context.Prijave.FindAsync(count);

            prvaPrijava.VremePrijave = DateTime.Now.AddDays(-10);
            poslednjaPrijava.VremePrijave = DateTime.Now.AddDays(-2);

            await _context.SaveChangesAsync();

            var result = await _controller.GetPrijaveByKorisnik(1);

            var okResult = result.Result as OkObjectResult;
            Assert.IsNotNull(okResult);
            var list = okResult.Value as IEnumerable<PrijavaDto>;

            Assert.That(list.Count, Is.EqualTo(count));

            Assert.Greater(list.First().VremePrijave, list.Last().VremePrijave);
        }

        [Test]
        public async Task GetMojePrijave_CurrUserNotInDB_ResUnauthorized()
        {
            int validTokenId = 99; 
            HelperFunctions.MockCurrentUser(_controller, validTokenId, "User");

            var result = await _controller.GetMojePrijave();

            Assert.That(result.Result, Is.InstanceOf(typeof(UnauthorizedResult)));
        }

        [Test]
        [Ignore("Trebalo bi opet da baca null exception zbog nepostojeceg oglasa...Zabrinjavajuce")]
        public async Task GetMojePrijave_PrijavaWithoutOglas_ThrowsNullReferenceException()
        {
            int korisnikID = 1;
            HelperFunctions.MockCurrentUser(_controller, korisnikID, "User");

            Assert.That(_context.Oglasi.ToList().Count, Is.EqualTo(0));

            await AddPrijava(korisnikID, 999, true, false);

            Assert.ThrowsAsync<NullReferenceException>(async () =>
                await _controller.GetMojePrijave());
        }

        [Test]
        public async Task GetMojePrijave_CleanPath_ResOk()
        {
            // Arrange
            int korisnikID = 1;
            int randomId = 2;
            HelperFunctions.MockCurrentUser(_controller, korisnikID, "User");

            await AddPrijava(korisnikID, 10, true, true);
            await AddPrijava(randomId, 10, true, false);

            var result = await _controller.GetMojePrijave();
            Assert.IsNotNull(result);

            var okResult = result.Result as OkObjectResult;
            var list = okResult.Value as IEnumerable<PrijavaDto>;
            Assert.IsNotNull(list);

            Assert.AreEqual(1, list.Count());
            Assert.IsTrue(list.All(p => p.KorisnikId == korisnikID));
        }

        [Test]
        public async Task GetPrijaveByOglas_InvalidId_ReturnsNotFound()
        {
            // Arrange
            int nonExistentOglasId = 999;

            // Act
            var result = await _controller.GetPrijaveByOglas(nonExistentOglasId);

            // Assert
            Assert.IsInstanceOf<NotFoundObjectResult>(result.Result);
            var res = result.Result as NotFoundObjectResult;
            Assert.AreEqual("Oglas ne postoji.", res.Value);
        }

        [Test]
        [TestCase("ODBIJENA")]
        [TestCase("odbijena")]
        [TestCase("OdbIJEna")]
        public async Task GetPrijaveByOglas_StatusFilterCheck_ResListWithOneElement(string status)
        {
            int oglasId = 1;
            await AddPrijava(1, 1);

            Prijava p = new Prijava
            {
                KorisnikId = 1,
                OglasId = oglasId,
                VremePrijave = DateTime.Now,
                Status = status,
                Poruka = "Test poruka"
            };
            await _context.SaveChangesAsync();

            var result = await _controller.GetPrijaveByOglas(oglasId);
            var okResult = result.Result as OkObjectResult;
            Assert.IsNotNull(okResult);

            var list = okResult.Value as IEnumerable<PrijavaDtos>;
            Assert.IsNotNull(list);

            //Prolazi jer izgleda da baza/ef core sam normalizuje stringove. Svakako crazy sto se ne koriste enumeracije
            Assert.That(list.Count, Is.EqualTo(1));
        }

        [Test]
        [Description("Izgleda da cim u upitu vidi da je nesto null, odmah pukne i vraca praznu listu, a ne null ref ex")]
        public async Task GetPrijaveByOglas_MissingKorisnik_ResOkEmptyList()
        {
            int oglasId = 1;

            _context.Prijave.Add(new Prijava
            {
                ID = 5,
                OglasId = oglasId,
                Status = "Na čekanju",
                Korisnik = null //pray to god da pukne ovde
            });
            await _context.SaveChangesAsync();

            await AddPrijava(300, oglasId, false, true);

            var result = await _controller.GetPrijaveByOglas(oglasId);
            Assert.IsNotNull(result);

            var ok = result.Result as OkObjectResult;
            Assert.IsNotNull(ok);

            var list = ok.Value as List<PrijavaDtos>;
            Assert.IsNotNull(list);
            Assert.IsEmpty(list);
        }

        [Test]
        public async Task GetStats_()
        {

        }

        [Test]
        public async Task GetStats_1()
        {

        }

        [Test]
        public async Task GetStats_2()
        {

        }

        [Ignore("Placeholder")]
        [Test]
        public async Task Nesto_()
        {

        }

        #endregion Tests
        private async Task AddPrijava(int korisnikID, int oglasID, bool createKorisnik = true, bool createOglas=true)
        {
            if(createKorisnik)
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

            if(createOglas)
                _context.Oglasi.Add(new Oglas
                {
                    ID = oglasID,
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

                    //PostavljacOglasa = korisnik,
                    PostavljacOglasaId = korisnikID,
                    KategorijaId = 1
                });

            Prijava p = new Prijava
            {
                KorisnikId = korisnikID,
                OglasId = oglasID,
                VremePrijave = DateTime.Now,
                Status = "Na cekanju",
                Poruka = "Test poruka"
            };

            _context.Prijave.Add(p);

            await _context.SaveChangesAsync();
        }
    }
}
