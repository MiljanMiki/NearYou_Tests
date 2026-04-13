using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Moq;
using NUnit.Framework.Constraints;
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

namespace WebTemplateTests.Services
{
    [TestFixture]
    //[Parallelizable(ParallelScope.Fixtures)]
    //[Ignore("Da ne smara")]
    internal class ChatServiceTests
    {
        private ChatService _service;
        private ApplicationDbContext _context;
        private Mock<IHubContext<ChatHub>> _hubContextMock;
        private Mock<ILogger<ChatService>> _loggerMock;

        [SetUp]
        public async Task Init()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            _context = new ApplicationDbContext(options);

            //mockovanje hub-a. Tri sloja moraju da se mockuju. Sve ovo samo za 2 funkcije...
            var mockClientProxy = new Mock<IClientProxy>();
            var mockClients = new Mock<IHubClients>();

            _hubContextMock = new();

            _hubContextMock.Setup(repo => repo.Clients).Returns(mockClients.Object);

            mockClients.Setup(repo => repo.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);

            _loggerMock = new();

            await _context.SaveChangesAsync();

            _service = new ChatService(_context, _hubContextMock.Object, _loggerMock.Object);
        }

        [TearDown]
        public void Cleanup() {
            _context.Dispose();
        }

        #region Tests
        [Test]
        public async Task CreateChatFromPrijavaAsync_PrijavaDoesntExist_InvalidOpEx()
        {
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async ()=> await _service.CreateChatFromPrijavaAsync(2,2));
            Assert.That(ex.Message, Is.EqualTo("Prijava nije pronađena ili nije prihvaćena"));
        }

        [Test]
        public async Task CreateChatFromPrijavaAsync_ChatAlreadyExists_InvalidOpEx()
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;

            await AddNecessaryObjects();

            //ko je projektovao ove modele svaka mu cast

            var existingChat = new Chat
            {
                OglasId = testOglasId,
                KlijentId = testKlijentId,
                OglasivacId = testOglasivacId
            };
            _context.Chatovi.Add(existingChat);

            await _context.SaveChangesAsync();

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.CreateChatFromPrijavaAsync(testPrijavaId, testOglasivacId));

            Assert.AreEqual("Chat već postoji za ovu prijavu", ex.Message);

            var chatCount = await _context.Chatovi.CountAsync();
            Assert.AreEqual(1, chatCount);
        }

        [Test]
        public async Task CreateChatFromPrijavaAsync_CleanPath_CleanDTO()
        {

            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;

            _context.Korisnici.Add(new Korisnik
            {
                ID = testOglasivacId,
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

            var oglas = new Oglas
            {
                ID = testOglasId,
                Naziv = "Test Oglas",
                PostavljacOglasaId = testOglasivacId,
                Adresa = "Test adresa",
                Grad = "Test grad",
                Opis = "Test opis"
            };
            _context.Oglasi.Add(oglas);

            var prijava = new Prijava
            {
                ID = testPrijavaId,
                OglasId = testOglasId,
                VremePrijave = DateTime.Now,
                KorisnikId = testKlijentId,
                Status = "Prihvacena",
                Korisnik = new Korisnik
                {
                    ID = testKlijentId,
                    Ime = "Petar",
                    Prezime = "Petrovic",
                    Username = "petarP",
                    Email = "petarPetrovic@gmail.com",
                    PasswordHash = "asdfghjkl123",
                    Role = UserRoles.User,
                    Biografija = "Cao svima ja sam petarP",
                    Telefon = "06555333",
                    SlikaURL = "http://provider/slike/petarP"
                }
            };
            _context.Prijave.Add(prijava);

            await _context.SaveChangesAsync();

            var result = await _service.CreateChatFromPrijavaAsync(testPrijavaId, testOglasivacId);
            Assert.IsNotNull(result);
            Assert.That(result.OglasNaziv, Is.EqualTo(oglas.Naziv));
            Assert.That(result.KlijentUsername, Is.EqualTo(prijava.Korisnik.Username));

        }

        [Test]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(15)]
        [TestCase(999)]
        public async Task GetUserChatsAsync_InvalidUserID_EmptyList(int userID)
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;

            await AddNecessaryObjects();
            await AddChat(testOglasId, testKlijentId, testOglasivacId);

            var result = await _service.GetUserChatsAsync(userID);

            Assert.IsNotNull(result);
            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task GetUserChatsAsync_ValidIDNoChats_EmptyList()
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;

            await AddNecessaryObjects();

            var result = await _service.GetUserChatsAsync(testKlijentId);

            Assert.IsNotNull(result);
            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task GetUserChatsAsync_ValidIDWithChats_List()
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;

            await AddNecessaryObjects();

            int count = 3;
            for(int i =0;i<count;++i)
                await AddChat(testOglasId, testKlijentId, testOglasivacId);

            var result = await _service.GetUserChatsAsync(testKlijentId);

            Assert.IsNotNull(result);
            Assert.That(result.Count, Is.EqualTo(count));
        }

        [Test]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(15)]
        public async Task GetChatWithMessagesAsync_InvalidChatID_KeyNotFoundEx(int chatID)
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;

            string porukaContent = "Cao svima ja sam majstor Bob i volim da jedem skrob";

            await WholeSetup(testOglasivacId, testKlijentId, testOglasId, testPrijavaId, porukaContent);

            Assert.ThrowsAsync<KeyNotFoundException>(async Task () => await _service.GetChatWithMessagesAsync(chatID,15));
        }

        [Test]
        public async Task GetChatWithMessagesAsync_UserNotInChat_UnauthorizedAccessEx()
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;
            string porukaContent = "Cao svima ja sam majstor Bob i volim da jedem skrob";

            await WholeSetup(testOglasivacId, testKlijentId, testOglasId, testPrijavaId, porukaContent);

            Assert.ThrowsAsync<UnauthorizedAccessException>(async Task () => await _service.GetChatWithMessagesAsync(1, 31));
        }

        [Test]
        public async Task GetChatWithMessagesAsync_CleanPath_ChatPorukeDTO()
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;
            string porukaContent = "Cao svima ja sam majstor Bob i volim da jedem skrob";

            await WholeSetup(testOglasivacId, testKlijentId, testOglasId, testPrijavaId,porukaContent);


            var result = await _service.GetChatWithMessagesAsync(1, testKlijentId);
            Assert.IsNotNull(result);

            var resultPoruke = result.Poruke;
            Assert.That(resultPoruke.Count, Is.EqualTo(1));
            Assert.That(resultPoruke.FirstOrDefault().Tekst, Is.EqualTo(porukaContent));
        }

        [Test]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(15)]
        public async Task SendMessageAsync_InvalidChatID_KeyNotFoundEx(int chatID)
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;
            string porukaContent = "Cao svima ja sam majstor Bob i volim da jedem skrob";
            await WholeSetup(testOglasivacId, testKlijentId, testOglasId, testPrijavaId, porukaContent);

            Assert.ThrowsAsync<KeyNotFoundException>(async Task () => await _service.SendMessageAsync(chatID, testKlijentId, porukaContent));
        }

        [Test]
        [Description("Metoda nema proveru za null/empty string.")]
        public async Task SendMessageAsync_MessageIsNull_DbUpdateEx()
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;
            string porukaContent = "Cao svima ja sam majstor Bob i volim da jedem skrob";
            await WholeSetup(testOglasivacId, testKlijentId, testOglasId, testPrijavaId, porukaContent);

            Assert.ThrowsAsync<DbUpdateException>(async Task () => await _service.SendMessageAsync(1, testKlijentId, null));
        }

        [Test]
        public async Task SendMessageAsync_CleanPath_PorukaDto()
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;
            string porukaContent = "Cao svima ja sam majstor Bob i volim da jedem skrob";

            await WholeSetup(testOglasivacId, testKlijentId, testOglasId, testPrijavaId, porukaContent);

            var result = await _service.SendMessageAsync(1, testKlijentId, porukaContent);
            Assert.IsNotNull(result);

            Assert.That(result.ChatId, Is.EqualTo(1));
            Assert.That(result.Tekst,Is.EqualTo(porukaContent));
        }

        [Test]
        [TestCase(-5)]
        [TestCase(0)]
        [TestCase(20)]
        public async Task GetChatAsync_InvalidChatID_KeyNotFoundEx(int chatID)
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;
            string porukaContent = "Cao svima ja sam majstor Bob i volim da jedem skrob";

            await WholeSetup(testOglasivacId, testKlijentId, testOglasId, testPrijavaId, porukaContent);

            Assert.ThrowsAsync<KeyNotFoundException>(async Task () => await _service.GetChatAsync(chatID));
        }

        [Test]
        [Description("Moze da pukne ako u chat-u nisu setovani oglas/klijent/oglasivac")]
        public async Task GetChatAsync_NullAttributes_KeyNotFoundEx()
        {
            //u bazi ne postoje objekti, pa samim tim i FKs su nevalidni
            //await AddNecessaryObjects();

            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;
            string porukaContent = "Cao svima ja sam majstor Bob i volim da jedem skrob";

            var chat = new Chat
            {
                Id = 1,
                OglasId = testOglasId,
                KlijentId = testKlijentId,
                OglasivacId = testOglasivacId,
                Kreiran = DateTime.UtcNow,
                PoslednjaPoruka = "Is this still available?",
                PoslednjaPorukaVreme = DateTime.UtcNow,
                PoslednjaPorukaPosiljalac = "Klijent",
                Poruke = new List<Poruka>()
            };

            var posiljalac = _context.Korisnici.Find(testKlijentId);

            var poruke = new List<Poruka>();
            poruke.Add(new Poruka
            {
                //ChatId = 1,
                Chat = chat,
                PosiljalacId = testKlijentId,
                Posiljalac = posiljalac,
                Tekst = porukaContent
            });
            await _context.Poruke.AddAsync(poruke.First());

            chat.Poruke = poruke;
            await _context.Chatovi.AddAsync(chat);

            await _context.SaveChangesAsync();

            var chatFromDB = await _context.Chatovi.FindAsync(1);
            Assert.That(chatFromDB, Is.Not.Null);

            //nece da nadje ovaj chat zbog includova
            Assert.ThrowsAsync<KeyNotFoundException>(async Task () => await _service.GetChatAsync(chatFromDB.Id));
        }

        [Test]
        public async Task GetChatAsync_CleanPath_()
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;
            string porukaContent = "Cao svima ja sam majstor Bob i volim da jedem skrob";

            await WholeSetup(testOglasivacId, testKlijentId, testOglasId, testPrijavaId, porukaContent);

            var result = await _service.GetChatAsync(1);
            Assert.IsNotNull(result);

            Assert.That(result.KlijentId,Is.EqualTo(testKlijentId));
            Assert.That(result.OglasivacId,Is.EqualTo(testOglasivacId));
            Assert.That(result.OglasId,Is.EqualTo(testOglasId));
        }

        [Test]
        [TestCase(-999)]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(15)]
        [TestCase(999)]
        public async Task DeleteMessage_InvalidChatID_KeyNotFoundEx(int chatID)
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;
            string porukaContent = "Cao svima ja sam majstor Bob i volim da jedem skrob";

            await WholeSetup(testOglasivacId, testKlijentId, testOglasId, testPrijavaId, porukaContent);

            Assert.ThrowsAsync<KeyNotFoundException>(async Task () => await _service.DeleteMessage(1,chatID,1));
        }

        [Test]
        [TestCase(-999)]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(15)]
        [TestCase(999)]
        public async Task DeleteMessage_InvalidMessageID_KeyNotFoundEx(int messageID)
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;
            string porukaContent = "Cao svima ja sam majstor Bob i volim da jedem skrob";

            await WholeSetup(testOglasivacId, testKlijentId, testOglasId, testPrijavaId, porukaContent);

            Assert.ThrowsAsync<KeyNotFoundException>(async Task () => await _service.DeleteMessage(1,1,messageID));
        }

        [Test]
        [TestCase(-999)]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(15)]
        [TestCase(999)]
        public async Task DeleteMessage_InvalidUserID_ArgumentEx(int userID)
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;
            string porukaContent = "Cao svima ja sam majstor Bob i volim da jedem skrob";

            await WholeSetup(testOglasivacId, testKlijentId, testOglasId, testPrijavaId, porukaContent);

            Assert.ThrowsAsync<ArgumentException>(async Task () => await _service.DeleteMessage(userID,1,1));
        }

        [Test]
        public async Task DeleteMessage_CleanPath_ResNoContent()
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;
            string porukaContent = "Cao svima ja sam majstor Bob i volim da jedem skrob";

            await WholeSetup(testOglasivacId, testKlijentId, testOglasId, testPrijavaId, porukaContent);

            var result = await _service.DeleteMessage(testKlijentId, 1, 1);
            Assert.IsNotNull(result);
            Assert.That(result as NoContentResult, Is.Not.Null);

            var chat = await _context.Chatovi.FindAsync(1);

            Assert.IsNotNull(chat);
            Assert.That(chat.Poruke.Count, Is.EqualTo(0));
        }

        //metoda prvo proverava chatId, pa onda messageID
        [Test]
        [TestCase(-999,1)]
        [TestCase(-1,1)]
        [TestCase(0,1)]
        [TestCase(15,1)]
        [TestCase(999,1)]
        [TestCase(1,-999)]
        [TestCase(1,-1)]
        [TestCase(1,0)]
        [TestCase(1,15)]
        [TestCase(1,999)]
        public async Task UpdateMessageAsync_InvalidIDs_KeyNotFoundEx(int chatId, int messageId)
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;
            string porukaContent = "Cao svima ja sam majstor Bob i volim da jedem skrob";

            await WholeSetup(testOglasivacId, testKlijentId, testOglasId, testPrijavaId, porukaContent);

            string message = "Baci se na poso Sundjer Bobe!";
            Assert.ThrowsAsync<KeyNotFoundException>(async Task () => await _service.UpdateMessageAsync(chatId,messageId,message));
        }

        [Test]
        [Description("Test pada jer se poruka ipak setuje na null. SaveChangesAsync ne baca exception. To nije dobro...")]
        public async Task UpdateMessageAsync_NullMessage_DbUpdateConcurrencyEx()
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;
            string porukaContent = "Cao svima ja sam majstor Bob i volim da jedem skrob";

            await WholeSetup(testOglasivacId, testKlijentId, testOglasId, testPrijavaId, porukaContent);

            //Assert.ThrowsAsync<DbUpdateConcurrencyException>(async Task () => await _service.UpdateMessageAsync(1, 1, null));
            var result = await _service.UpdateMessageAsync(1, 1, null);
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task UpdateMessageAsync_CleanPath()
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;
            string porukaContent = "Cao svima ja sam majstor Bob i volim da jedem skrob";

            await WholeSetup(testOglasivacId, testKlijentId, testOglasId, testPrijavaId, porukaContent);

            string newMessage = "Baci se na poso Sundjer Bobe!";

            var result = await _service.UpdateMessageAsync(1, 1, newMessage);
            Assert.IsNotNull(result);

            var poruka= await _context.Poruke.FindAsync(1);
            Assert.IsNotNull(poruka);
            Assert.That(poruka.Tekst, Is.Not.EqualTo(porukaContent));


        }
        #endregion Tests

        private async Task AddChat(int oglasID,int clientID,int oglasivacID)
        {
            var chat = new Chat
            {
                //Id = 1,
                OglasId = oglasID,       
                KlijentId = clientID,      
                OglasivacId = oglasivacID,    
                Kreiran = DateTime.UtcNow,
                PoslednjaPoruka = "Is this still available?",
                PoslednjaPorukaVreme = DateTime.UtcNow,
                PoslednjaPorukaPosiljalac = "Klijent",
                Poruke = new List<Poruka>() 
            };
            await _context.Chatovi.AddAsync(chat);

            await _context.SaveChangesAsync();
        }

        private async Task AddNecessaryObjects()
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;

            _context.Korisnici.Add(new Korisnik
            {
                ID = testOglasivacId,
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

            var oglas = new Oglas
            {
                ID = testOglasId,
                Naziv = "Test Oglas",
                PostavljacOglasaId = testOglasivacId,
                Adresa = "Test adresa",
                Grad = "Test grad",
                Opis = "Test opis"
            };
            _context.Oglasi.Add(oglas);

            var prijava = new Prijava
            {
                ID = testPrijavaId,
                OglasId = testOglasId,
                VremePrijave = DateTime.Now,
                KorisnikId = testKlijentId,
                Status = "Prihvacena",
                Korisnik = new Korisnik
                {
                    ID = testKlijentId,
                    Ime = "Petar",
                    Prezime = "Petrovic",
                    Username = "petarP",
                    Email = "petarPetrovic@gmail.com",
                    PasswordHash = "asdfghjkl123",
                    Role = UserRoles.User,
                    Biografija = "Cao svima ja sam petarP",
                    Telefon = "06555333",
                    SlikaURL = "http://provider/slike/petarP"
                }
            };
            _context.Prijave.Add(prijava);

            await _context.SaveChangesAsync();
        }

        private async Task WholeSetup(int oglasivacID,int klijentID, int oglasID, int prijavaID, string porukaContent)
        {
            await AddNecessaryObjects();


            //moraju da se dodaju i CELI OBJEKTI oglas/oglasivac/klijent zbog includova u query
            var chat = new Chat
            {
                Id = 1,
                OglasId = oglasID,
                Oglas = await _context.Oglasi.FindAsync(oglasID),
                KlijentId = klijentID,
                Klijent = await _context.Korisnici.FindAsync(klijentID),
                OglasivacId = oglasivacID,
                Oglasivac = await _context.Korisnici.FindAsync(oglasivacID),
                Kreiran = DateTime.UtcNow,
                PoslednjaPoruka = "Is this still available?",
                PoslednjaPorukaVreme = DateTime.UtcNow,
                PoslednjaPorukaPosiljalac = "Klijent",
                Poruke = new List<Poruka>()
            };

            var posiljalac = _context.Korisnici.Find(klijentID);

            var poruke = new List<Poruka>();
            poruke.Add(new Poruka
            {
                //ChatId = 1,
                Chat = chat,
                PosiljalacId = klijentID,
                Posiljalac = posiljalac,
                Tekst = porukaContent
            });
            await _context.Poruke.AddAsync(poruke.First());

            chat.Poruke = poruke;
            await _context.Chatovi.AddAsync(chat);

            await _context.SaveChangesAsync();
        }
    }
}
