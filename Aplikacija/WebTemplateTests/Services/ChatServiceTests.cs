using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
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

namespace WebTemplateTests.Services
{
    [TestFixture]
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

            _hubContextMock = new();
            //_hubContextMock.Setup(repo => repo.Clients.Group(It.IsAny<string>())).Returns();
            //_hubContextMock.Setup(repo => repo.Clients.Group(It.IsAny<string>())).Returns();
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
        public void CreateChatFromPrijavaAsync_PrijavaDoesntExist_InvalidOpEx()
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
        public async Task GetUserChatsAsync_InvalidUserID_EmptyList()
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;

            await AddNecessaryObjects();
            await AddChat(testOglasId, testKlijentId, testOglasivacId);

            var result = await _service.GetUserChatsAsync(999);

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
        public async Task GetChatWithMessagesAsync_InvalidChatID_KeyNotFoundEx()
        {
            int testOglasivacId = 10;
            int testKlijentId = 5;
            int testOglasId = 100;
            int testPrijavaId = 1;

            await AddNecessaryObjects();

            
            var chat = new Chat
            {
                //Id = 1,
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
                ChatId = 1,
                Chat = chat,
                PosiljalacId=testKlijentId,
                Posiljalac = posiljalac,
                Tekst="Cao svima ja sam majstor Bob i volim da jedem skrob"
            });
            await _context.Poruke.AddAsync(poruke.First());

            chat.Poruke = poruke;
            await _context.Chatovi.AddAsync(chat);

            await _context.SaveChangesAsync();

            Assert.ThrowsAsync<KeyNotFoundException>(async Task () => await _service.GetChatWithMessagesAsync(15,15));
        }

        [Test]
        public async Task GetChatWithMessagesAsync_UserNotInChat_UnauthorizedAccessEx()
        {

        }

        [Test]
        public async Task GetChatWithMessagesAsync_CleanPath_ChatPorukeDTO()
        {

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

    }
}
