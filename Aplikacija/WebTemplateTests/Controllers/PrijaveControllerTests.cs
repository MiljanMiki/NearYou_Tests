using Castle.Core.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebTemplate.Controllers;
using WebTemplate.DTOs;
using WebTemplate.Hubs;
using WebTemplate.Models;
using WebTemplate.Services;

namespace WebTemplateTests.Controllers
{
    [TestFixture]
    //[Parallelizable(ParallelScope.Fixtures)]
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
        [Description("Trebalo bi da pukne jer ne postoji NavData za Oglas i korisnika, ali nesto nece. Hmmm...")]
        public async Task GetPrijaveByKorisnik_MissingNavData_ResOkEmptyList()
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

            var result = await _controller.GetPrijaveByKorisnik(targetKorisnikId);
            Assert.IsInstanceOf<OkObjectResult>(result.Result);

            var ok = result.Result as OkObjectResult;
            var lista = ok.Value as List<PrijavaDto>;

            Assert.That(lista, Is.Empty);
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
            HelperFunctions.MockCurrentUser(_controller, validTokenId, UserRoles.User);

            var result = await _controller.GetMojePrijave();

            Assert.That(result.Result, Is.InstanceOf(typeof(UnauthorizedResult)));
        }

        [Test]
        [Description("Trebalo bi opet da baca null exception zbog nepostojeceg oglasa...Zabrinjavajuce")]
        public async Task GetMojePrijave_PrijavaWithoutOglas_ResOkEmptyList()
        {
            int korisnikID = 1;
            HelperFunctions.MockCurrentUser(_controller, korisnikID, "User");

            Assert.That(_context.Oglasi.ToList().Count, Is.EqualTo(0));

            await AddPrijava(korisnikID, 999, true, false);

            var result = await _controller.GetMojePrijave();
            Assert.IsInstanceOf<OkObjectResult>(result.Result);

            var ok = result.Result as OkObjectResult;
            var lista = ok.Value as List<PrijavaDto>;

            Assert.That(lista, Is.Empty);
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
        [TestCase("Na cekanju")]
        [TestCase("NA CEKANJU")]
        [TestCase("na cekanju")]
        public async Task GetStats_EncodingCheckNaCekanju_ResOkZeroCount(string status)
        {
            int count = 10;
            for (int i = 1; i <= count; ++i)
                await AddPrijava(i, i, true, true, status);//10 razlicitih korisnika i oglasa sa 10 prijava

            var result = await _controller.GetStats();
            Assert.IsNotNull(result);

            var ok = result.Result as OkObjectResult;
            Assert.IsNotNull(ok);

            var value = ok.Value as PrijavaStatsDto;
            Assert.IsNotNull(value);
            Assert.That(value.NaCekanju, Is.EqualTo(0));
        }

        [Test]
        [TestCase("Prihvacena")]
        [TestCase("PRIHVACENA")]
        [TestCase("prihvacena")]
        public async Task GetStats_EncodingCheckPrihvacena_ResOkZeroCount(string status)
        {
            int count = 10;
            for (int i = 1; i <= count; ++i)
                await AddPrijava(i, i, true, true, status);//10 razlicitih korisnika i oglasa sa 10 prijava

            var result = await _controller.GetStats();
            Assert.IsNotNull(result);

            var ok = result.Result as OkObjectResult;
            Assert.IsNotNull(ok);

            var value = ok.Value as PrijavaStatsDto;
            Assert.IsNotNull(value);
            Assert.That(value.Prihvacene, Is.EqualTo(0));
        }

        [Test]
        [TestCase(0)]
        [TestCase(10)]
        public async Task GetStats_CleanPath_ResOkTotalCount(int count)
        {
            for (int i = 1; i <= count; ++i)
                await AddPrijava(i, i, true, true);//10 razlicitih korisnika i oglasa sa 10 prijava

            var result = await _controller.GetStats();
            Assert.IsNotNull(result);

            var ok = result.Result as OkObjectResult;
            Assert.IsNotNull(ok);

            var value = ok.Value as PrijavaStatsDto;
            Assert.IsNotNull(value);
            Assert.That(value.UkupnoPrijava, Is.EqualTo(count));
        }

        [Test]
        [Description("Nema provera da li korisnik uopste postoji u bazi")]
        public async Task PostPrijava_CurrUserExistsButNotInDb_AddsPrijava()
        {
            int userId= 99;
            HelperFunctions.MockCurrentUser(_controller, userId, UserRoles.User);

            await AddPrijava(1, 1, false);


            var dto = new PrijavaCreateDto { OglasId = 1, Poruka = "Test oglas poruka" };

            var result = await _controller.PostPrijava(dto);
            Assert.That(result, Is.InstanceOf<OkObjectResult>());

            var prijave = await _context.Prijave.ToListAsync();
            Assert.That(prijave.Count, Is.EqualTo(2),"Dodaje se prijava za nepostojeceg korisnika");
        }

        [Test]
        [Description("Ako ovaj test prodje, znaci da moze da se postavi prijava za obrisan oglas.")]
        public async Task PostPrijava_DeletedOglas_AddsPrijava()
        {
            int korisnikID = 1;
            HelperFunctions.MockCurrentUser(_controller, korisnikID, UserRoles.User);

            await AddPrijava(korisnikID, 1);

            _context.Oglasi.Add(new Oglas
            {
                ID = 2,
                Naziv = "TestNaziv",
                Opis = "TestOpis",
                Adresa = "TestAdresa",
                Grad = "Test",
                Latitude = 0.0,
                Longitude = 0.0,
                Cena = 999,
                TipCene = TipCene.Fiksno,

                Status = "Obrisan",//ili obrisan, OBRISAN, obRisAN...i to isto vrv puca
                DatumKreiranja = DateTime.UtcNow,

                PostavljacOglasaId = 2,
                KategorijaId = 1
            });

            await _context.SaveChangesAsync();

            var dto = new PrijavaCreateDto { OglasId = 2, Poruka = "Test poruka" };

            // Act
            var result = await _controller.PostPrijava(dto);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());//laksa je ova provera

            var prijavaDb = _context.Prijave.FirstOrDefault(p => p.OglasId == 2);
            Assert.IsNotNull(prijavaDb,"Po logici nekoj, ne bi trebalo da moze da se doda prijava za obrisan oglas.");
        }

        [Test]

        public async Task PostPrijava_CleanPath_ResOk()
        {
            int korisnikId = 1, oglasId = 1;
            await AddPrijava(korisnikId, oglasId);
            HelperFunctions.MockCurrentUser(_controller, korisnikId, UserRoles.User);

            _context.Oglasi.Add(new Oglas
            {
                ID = 2,
                Naziv = "TestNaziv",
                Opis = "TestOpis",
                Adresa = "TestAdresa",
                Grad = "Test",
                Latitude = 0.0,
                Longitude = 0.0,
                Cena = 999,
                TipCene = TipCene.Fiksno,

                Status = "Obrisan",//ili obrisan, OBRISAN, obRisAN...i to isto vrv puca
                DatumKreiranja = DateTime.UtcNow,

                PostavljacOglasaId = 2,
                KategorijaId = 1
            });

            var dto = new PrijavaCreateDto
            {
                OglasId = 2,
                Poruka = "Test poruka"
            };

            var result = await _controller.PostPrijava(dto);
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<OkObjectResult>(result);

            var prijave = await _context.Prijave.ToListAsync();
            Assert.IsNotNull(prijave);
            Assert.That(prijave.Count, Is.EqualTo(2));
        }

        [Test]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(999)]
        public async Task PutPrijava_InvalidIDs_ResNotFound(int id)
        {
            for(int i =1;i<10;++i)
                await AddPrijava(i,i);

            var dto = new PrijavaUpdateDto
            {
                Poruka = "Updated test poruka",
                Status = "Updated status"
            };

            var result = await _controller.PutPrijava(id, dto);
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<NotFoundObjectResult>(result);
            Assert.That((result as NotFoundObjectResult).StatusCode, Is.EqualTo(404));
        }

        [Test]
        public async Task PutPrijava_NullDTO_NullRefEx()
        {
            for (int i = 1; i < 10; ++i)
                await AddPrijava(i, i);

            var dto = new PrijavaUpdateDto
            {
                Poruka = "Updated test poruka",
                Status = "Updated status"
            };

            Assert.ThrowsAsync<NullReferenceException>(async Task () => await _controller.PutPrijava(1, null));
        }

        [Test]
        public async Task PutPrijava_CleanPath_ResNoContent()
        {
            for (int i = 1; i < 10; ++i)
                await AddPrijava(i, i);

            string updatedPoruka = "Updated test poruka";
            string updatedStatus = "Updated status";
            var dto = new PrijavaUpdateDto
            {
                Poruka = updatedPoruka,
                Status = updatedStatus
            };

            int updatedID = 1;
            var result = await _controller.PutPrijava(updatedID, dto);
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<NoContentResult>(result);

            var updatedPrijava = await _context.Prijave.FindAsync(updatedID);
            Assert.IsNotNull(updatedPrijava);
            Assert.That(updatedPrijava.Poruka, Is.EqualTo(updatedPoruka));
            Assert.That(updatedPrijava.Status, Is.EqualTo(updatedStatus));
        }

        [Test]
        public async Task UpdatePrijavaStatus_NullDTO_NullRefEx()
        {
            int korisnikID = 1;
            HelperFunctions.MockCurrentUser(_controller, korisnikID, UserRoles.User);
            await AddPrijava(korisnikID, 1);


            Assert.ThrowsAsync<NullReferenceException>(async Task () => await _controller.UpdatePrijavaStatus(1, null));
        }

        [Test]
        public async Task UpdatePrijavaStatus_FailedChatCreation_ResOkChatNotCreated()
        {
            int korisnikID = 1;

            MockingForUpdateStatus(korisnikID, true);

            await AddPrijava(korisnikID, 1);

            var dto = new PrijavaStatusUpdateDto { Status = "Prihvacena" };

            var result = await _controller.UpdatePrijavaStatus(1, dto);
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<OkObjectResult>(result);

            var message = (result as OkObjectResult).Value;
            Assert.IsNotNull(message);
            var type = message.GetType();
            Assert.That(type.GetProperty("Message").GetValue(message, null), Is.EqualTo("Prijava prihvaćena, ali chat nije kreiran zbog greške"));
        }

        [Test]
        public async Task UpdatePrijavaStatus_CleanPath_ResOkChatCreated()
        {
            int korisnikID = 1;
            MockingForUpdateStatus(korisnikID, false);
            await AddPrijava(korisnikID, 1);

            var dto = new PrijavaStatusUpdateDto { Status = "Prihvacena" };

            var result = await _controller.UpdatePrijavaStatus(1, dto);
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<OkObjectResult>(result);

            var message = (result as OkObjectResult).Value;
            Assert.IsNotNull(message);
            var type = message.GetType();
            Assert.That(type.GetProperty("Message").GetValue(message,null), Is.EqualTo("Prijava prihvaćena i chat kreiran"));
        }

        [Test]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(999)]
        public async Task DeletePrijava_InvalidIDs_ResNotFound(int id)
        {
            for (int i = 1; i <= 10; ++i)
                await AddPrijava(i, i);

            var result = await _controller.DeletePrijava(id);
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<NotFoundObjectResult>(result);
            Assert.That((result as NotFoundObjectResult).StatusCode, Is.EqualTo(404));
        }

        [Test]
        public async Task DeletePrijava_CleanPath_ResNoContentDeletedPrijava()
        {
            for (int i = 1; i <= 10; ++i)
                await AddPrijava(i, i);

            int id = 1;
            var result = await _controller.DeletePrijava(id);
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<NoContentResult>(result);

            var prijave = await _context.Prijave.ToListAsync();
            Assert.IsNotNull(prijave);
            Assert.IsNotEmpty(prijave);
            Assert.That(prijave.Count, Is.EqualTo(9));

            Assert.IsNull(await _context.Prijave.FindAsync(id));
        }

        #endregion Tests

        private void MockingForUpdateStatus(int korisnikID, bool chatServiceThrows)
        {
            HelperFunctions.MockCurrentUser(_controller, korisnikID, UserRoles.User);//prvo mockujem user-a, koji napravi httpcontext
                                                                                     //zatim samo dodam na vec postojeci context.

            var serviceProviderMock = new Mock<IServiceProvider>();
            _controller.HttpContext.RequestServices = serviceProviderMock.Object;
            //_controller.ControllerContext = new ControllerContext
            //{
            //    HttpContext = new DefaultHttpContext { RequestServices = serviceProviderMock.Object }
            //};

            //mock za chat servis
            var mockChatService = new Mock<IChatService>();
            if(!chatServiceThrows)
                mockChatService.Setup(repo => repo.CreateChatFromPrijavaAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(new ChatDto { });
            else
                mockChatService.Setup(repo => repo.CreateChatFromPrijavaAsync(It.IsAny<int>(), It.IsAny<int>())).ThrowsAsync(new InvalidOperationException());

            serviceProviderMock.Setup(x => x.GetService(typeof(IChatService))).Returns(mockChatService.Object);

            //mock za IHubContext (3 sloja)
            var mockClientProxy = new Mock<IClientProxy>();
            var mockClients = new Mock<IHubClients>();

            Mock<IHubContext<ChatHub>> mockHubContext = new();
            mockHubContext.Setup(repo => repo.Clients).Returns(mockClients.Object);
            mockClients.Setup(repo => repo.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
            serviceProviderMock.Setup(x => x.GetService(typeof(IHubContext<ChatHub>))).Returns(mockHubContext.Object);

            //Mock za logger
            Mock<ILogger<PrijaveController>> mockLogger = new();
            serviceProviderMock.Setup(x => x.GetService(typeof(ILogger<PrijaveController>))).Returns(mockLogger.Object);
        }
        private async Task AddPrijava(int korisnikID, int oglasID, bool createKorisnik = true, bool createOglas=true,string status = "Na cekanju")
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
                Status = status,
                Poruka = "Test poruka"
            };

            _context.Prijave.Add(p);

            await _context.SaveChangesAsync();
        }
    }
}
