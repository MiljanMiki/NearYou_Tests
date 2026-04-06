using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebTemplate.DTOs;

namespace WebTemplate.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OglasiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public OglasiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Oglasi/Search
        /// Pribavlja podatke za prikaz liste/mape oglasa na glavnoj stranici aplikacije
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<OglasSearchDto>>> GetOglasi([FromQuery] OglasiSearchQuery query)
        {
            var oglasi = await _context.GetOglasiAsync(query);

            return Ok(oglasi);
        }

        
        // GET: api/Oglasi/Prikaz
        /// Pribavlja podatke za otvaranje modala sa svim informacijama jednog oglasa.
        [HttpGet("prikaz")]
        public async Task<ActionResult<OglasDto>> GetOglas([FromQuery] OglasPrikazQuery query)
        {
            var oglas = await _context.GetOglasPrikazAsync(query);

            return Ok(oglas);
        }

        // GET: api/Oglasi/Korisnik
        [HttpGet("Korisnik")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<OglasDto>>> GetOglasiByKorisnik()
        {
            var korisnikId = GetCurrentUserId();

            var oglasi = await _context.Oglasi
                .Include(o => o.PostavljacOglasa)
                .Where(o => o.PostavljacOglasa.ID == korisnikId && o.Status != "obrisan")
                .Select(o => new OglasDto
                {
                    ID = o.ID,
                    Naziv = o.Naziv,
                    Opis = o.Opis,
                    PostavljacOglasaId = o.PostavljacOglasa.ID,
                    PostavljacIme = $"{o.PostavljacOglasa.Ime} {o.PostavljacOglasa.Prezime}"
                })
                .ToListAsync();

            return Ok(oglasi);
        }

        //PUT: api/Oglasi/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutOglas(int id, OglasCreateDto oglasDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var oglas = await _context.Oglasi.FindAsync(id);
            if (oglas == null)
            {
                return NotFound();
            }


            oglas.Naziv = oglasDto.Naziv;
            oglas.Opis = oglasDto.Opis;
            oglas.Adresa = oglasDto.Adresa;
            oglas.Grad = oglasDto.Grad;
            oglas.Latitude = oglasDto.Latitude;
            oglas.Longitude = oglasDto.Longitude;
            oglas.Cena = oglasDto.Cena;
            oglas.TipCene = oglasDto.TipCene;
            //oglas.SlikaUrl = oglasDto.Slika.;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!OglasExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Oglasi
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<OglasDto>> PostOglas([FromForm]OglasCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Trenutno ulogovan korisnik
            var currentUserId = GetCurrentUserId();

            var korisnik = await _context.Korisnici.FindAsync(currentUserId);
            if (korisnik == null)
                return Unauthorized();

            // Provera kategorije
            var kategorija = await _context.Kategorije.FindAsync(dto.KategorijaId);
            if (kategorija == null)
                return BadRequest("Kategorija ne postoji");

            var oglas = new Oglas
            {
                Naziv = dto.Naziv,
                Opis = dto.Opis,
                Adresa = dto.Adresa,
                Grad = dto.Grad,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Cena = dto.Cena,
                TipCene = dto.TipCene,
               
                Status = "Postavljen",
                DatumKreiranja = DateTime.UtcNow,

                PostavljacOglasaId = korisnik.ID,
                KategorijaId = kategorija.Id
            };

            _context.Oglasi.Add(oglas);
            await _context.SaveChangesAsync();

            if (dto.Slika != null)
            {
                var folder = Path.Combine("wwwroot/images/oglasi", oglas.ID.ToString());
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
                var fileName = "cover" + Path.GetExtension(dto.Slika.FileName);
                var path = Path.Combine(folder, fileName);

                using var stream = new FileStream(path, FileMode.Create);
                await dto.Slika.CopyToAsync(stream);

                oglas.SlikaUrl = $"/images/oglasi/{oglas.ID}/{fileName}";
                await _context.SaveChangesAsync();
            }

            var result = new OglasDto
            {
                ID = oglas.ID,
                Naziv = oglas.Naziv,
                Opis = oglas.Opis,
                Adresa = oglas.Adresa,
                Grad = oglas.Grad,
                Latitude = oglas.Latitude,
                Longitude = oglas.Longitude,
                Cena = oglas.Cena,
                TipCene = oglas.TipCene,
                CenaDisplay = oglas.CenaDisplay,
                SlikaUrl = oglas.SlikaUrl,
                DatumKreiranja = oglas.DatumKreiranja,

                PostavljacOglasaId = korisnik.ID,
                PostavljacIme = $"{korisnik.Ime} {korisnik.Prezime}",

                KategorijaId = kategorija.Id,
                KategorijaNaziv = kategorija.Naziv
            };

            return CreatedAtAction(nameof(GetOglas), new { id = oglas.ID }, result);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteOglas(int id)
        {
            var oglas = await _context.Oglasi
                .Include(o => o.Prijave)
                .FirstOrDefaultAsync(o => o.ID == id);

            if (oglas == null)
                return NotFound("Oglas ne postoji");

            var currentUserId = GetCurrentUserId();
            var role = GetCurrentUserRole();

            if (oglas.PostavljacOglasaId != currentUserId && role != UserRoles.Admin)
                return Forbid();

            _context.Prijave.RemoveRange(oglas.Prijave);

            _context.Oglasi.Remove(oglas);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPut("obrisi/{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteOglasStatus(int id)
        {
            var oglas = await _context.Oglasi
                .Include(o => o.Prijave)
                .FirstOrDefaultAsync(o => o.ID == id);

            if (oglas == null)
                return NotFound("Oglas ne postoji");

            var currentUserId = GetCurrentUserId();
            var role = GetCurrentUserRole();

            if (oglas.PostavljacOglasaId != currentUserId && role != UserRoles.Admin)
                return Forbid();

            oglas.Status = "obrisan";

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {

            }

            return NoContent();
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        private string? GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value;
        }

        private bool KorisnikExists(int id)
        {
            return _context.Korisnici.Any(e => e.ID == id);
        }

        private bool OglasExists(int id)
        {
            return _context.Oglasi.Any(e => e.ID == id);
        }
    }
}
