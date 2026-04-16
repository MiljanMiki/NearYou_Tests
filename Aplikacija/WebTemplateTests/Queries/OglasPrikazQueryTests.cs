using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebTemplate.DTOs;
using WebTemplate.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace WebTemplateTests.Queries
{
    [TestFixture]
    internal class OglasPrikazQueryTests
    {
        private ApplicationDbContext _context;

        [SetUp]
        public async Task Init()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            _context = new ApplicationDbContext(options);

            await _context.SaveChangesAsync();
        }

        [TearDown]

        public void Cleanup()
        {
            _context.Dispose();
        }


        [Test]
        public async Task OglasPrikazQuery_EmptyDB_RetNull()
        {
            OglasPrikazQuery query = new OglasPrikazQuery { OglasId = 1 };
            var result = await _context.GetOglasPrikazAsync(query);

            Assert.IsNull(result);
        }

        [Test]
        public async Task OglasPrikazQuery_DBFullNullKorisnik_RetNull()
        {
            int korisnikID = 1;
            await AddOglasi(10, -1, 1,false);

            OglasPrikazQuery query = new OglasPrikazQuery { OglasId = 1 };
            var result = await _context.GetOglasPrikazAsync(query);

            Assert.IsNull(result);
        }

        [Test]
        public async Task OglasPrikazQuery_DBFull_CorrectDTO()
        {
            int count = 20;

            await AddOglasi(1, 1, 1);
            await AddOglasi(10, 1, 1,false);

            OglasPrikazQuery query = new OglasPrikazQuery { OglasId = 1 };
            var result = await _context.GetOglasPrikazAsync(query);

            Assert.IsNotNull(result);
            Assert.That(result.ID, Is.EqualTo(1));
            Assert.IsNull(result.SlikaOglasa);
        }

        private async Task AddOglasi(int count, int korisnikID, int kategorijaID, bool praviKorisnika = true)
        {
            Korisnik korisnik = null;
            if (praviKorisnika)
            {
                korisnik = new Korisnik
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
            }

            for (int i = 1; i <= count; ++i)
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
