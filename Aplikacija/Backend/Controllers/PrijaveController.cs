using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using WebTemplate.DTOs;
using WebTemplate.Hubs;
using WebTemplate.Models;
using WebTemplate.Services;

namespace WebTemplate.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PrijaveController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PrijaveController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Prijave
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PrijavaDto>>> GetPrijave()
        {

            var prijave = await _context.Prijave
                .Include(p => p.Korisnik)
                .Include(p => p.Oglas)
                .Select(p => new PrijavaDto
                {
                    ID = p.ID,
                    KorisnikId = p.KorisnikId,
                    KorisnikIme = $"{p.Korisnik.Ime} {p.Korisnik.Prezime}",
                    OglasId = p.OglasId,
                    OglasNaziv = p.Oglas.Naziv,
                    VremePrijave = p.VremePrijave,
                    Status = p.Status,
                    Poruka = p.Poruka
                })
                .OrderByDescending(p => p.VremePrijave)
                .ToListAsync();

            return Ok(prijave);
        }

        // GET: api/Prijave/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PrijavaDto>> GetPrijava(int id)
        {
            var prijava = await _context.Prijave
                .Include(p => p.Korisnik)
                .Include(p => p.Oglas)
                .Where(p => p.ID == id)
                .Select(p => new PrijavaDto
                {
                    ID = p.ID,
                    KorisnikId = p.KorisnikId,
                    KorisnikIme = $"{p.Korisnik.Ime} {p.Korisnik.Prezime}",
                    OglasId = p.OglasId,
                    OglasNaziv = p.Oglas.Naziv,
                    VremePrijave = p.VremePrijave,
                    Status = p.Status,
                    Poruka = p.Poruka
                })
                .FirstOrDefaultAsync();

            if (prijava == null)
            {
                return NotFound($"Prijava sa ID {id} nije pronađena.");
            }

            return Ok(prijava);
        }

        // GET: api/Prijave/Korisnik/5
        [HttpGet("Korisnik/{korisnikId}")]
        public async Task<ActionResult<IEnumerable<PrijavaDto>>> GetPrijaveByKorisnik(int korisnikId)
        {
            var prijave = await _context.Prijave
                .Include(p => p.Korisnik)
                .Include(p => p.Oglas)
                .Where(p => p.KorisnikId == korisnikId)
                .Select(p => new PrijavaDto
                {
                    ID = p.ID,
                    KorisnikId = p.KorisnikId,
                    KorisnikIme = $"{p.Korisnik.Ime} {p.Korisnik.Prezime}",
                    OglasId = p.OglasId,
                    OglasNaziv = p.Oglas.Naziv,
                    VremePrijave = p.VremePrijave,
                    Status = p.Status,
                    Poruka = p.Poruka
                })
                .OrderByDescending(p => p.VremePrijave)
                .ToListAsync();

            return Ok(prijave);
        }

        [HttpGet("Oglas/moje")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<PrijavaDto>>> GetMojePrijave()
        {
            var korisnikId = GetCurrentUserId();
            var korisnik = await _context.Korisnici.FindAsync(korisnikId);
            if (korisnik == null)
                return Unauthorized();

            var prijave = await _context.Prijave
                .Include(p => p.Korisnik)
                .Include(p => p.Oglas)
                .Where(p => p.KorisnikId == korisnikId)
                .Select(p => new PrijavaDto
                {
                    ID = p.ID,
                    KorisnikId = p.KorisnikId,
                    KorisnikIme = $"{p.Korisnik.Ime} {p.Korisnik.Prezime}",
                    OglasId = p.OglasId,
                    OglasNaziv = p.Oglas.Naziv,
                    VremePrijave = p.VremePrijave,
                    Status = p.Status,
                    Poruka = p.Poruka
                })
                .OrderByDescending(p => p.VremePrijave)
                .ToListAsync();

            return Ok(prijave);
        }
        // GET: api/Prijave/Oglas/5
        [HttpGet("Oglas/{oglasId}")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<PrijavaDtos>>> GetPrijaveByOglas(int oglasId)
        {
            var oglasPostoji = await _context.Oglasi.AnyAsync(o => o.ID == oglasId);
            if (!oglasPostoji)
                return NotFound("Oglas ne postoji.");

            var prijave = await _context.Prijave
                .Where(p => p.OglasId == oglasId)
                .Where(p => p.Status != "Odbijena")
                .Include(p => p.Korisnik)
                .OrderByDescending(p => p.VremePrijave)
                .Select(p => new PrijavaDtos
                {
                    ID = p.ID,
                    OglasId = p.OglasId,
                    VremePrijave = p.VremePrijave,
                    Status = p.Status,
                    Poruka = p.Poruka,

                    Korisnik = new PrijavaKorisnikDto
                    {
                        Id = p.Korisnik.ID,
                        Ime = p.Korisnik.Ime,
                        Prezime = p.Korisnik.Prezime,
                        Username = p.Korisnik.Username
                    }
                })
                .ToListAsync();

            return Ok(prijave);
        }

        // GET: api/Prijave/Stats
        [HttpGet("Stats")]
        public async Task<ActionResult<PrijavaStatsDto>> GetStats()
        {
            var stats = new PrijavaStatsDto
            {
                UkupnoPrijava = await _context.Prijave.CountAsync(),
                NaCekanju = await _context.Prijave.CountAsync(p => p.Status == "Na čekanju"),
                Prihvacene = await _context.Prijave.CountAsync(p => p.Status == "Prihvaćena"),
                Odbijene = await _context.Prijave.CountAsync(p => p.Status == "Odbijena")
            };

            return Ok(stats);
        }

        // POST: api/Prijave
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> PostPrijava(PrijavaCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var korisnikId = GetCurrentUserId();
            if (korisnikId == null)
                return Unauthorized("Korisnik nije autentifikovan");

            var oglas = await _context.Oglasi.FindAsync(dto.OglasId);
            if (oglas == null)
                return BadRequest("Oglas nije pronađen.");

            var postojiPrijava = await _context.Prijave
                .AnyAsync(p => p.KorisnikId == korisnikId && p.OglasId == dto.OglasId);

            if (postojiPrijava)
                return Conflict("Već ste prijavljeni na ovaj oglas.");

            if (korisnikId.Value == oglas.PostavljacOglasaId)
            {
                return Conflict("Ne možete se prijaviti na sopstveni oglas");
            }

            var prijava = new Prijava
            {
                KorisnikId = korisnikId.Value,
                OglasId = dto.OglasId,
                Poruka = dto.Poruka,
                VremePrijave = DateTime.Now,
                Status = "Na čekanju"
            };

            _context.Prijave.Add(prijava);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = "Prijava uspešno poslata",
                PrijavaId = prijava.ID,
                Status = prijava.Status,
                VremePrijave = prijava.VremePrijave
            });
        }

        // PUT: api/Prijave/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPrijava(int id, PrijavaUpdateDto prijavaDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var prijava = await _context.Prijave.FindAsync(id);
            if (prijava == null)
            {
                return NotFound($"Prijava sa ID {id} nije pronađena.");
            }

            prijava.Status = prijavaDto.Status;
            if (!string.IsNullOrEmpty(prijavaDto.Poruka))
            {
                prijava.Poruka = prijavaDto.Poruka;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PrijavaExists(id))
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
        [HttpPut("{id}/status")]
        [Authorize]
        public async Task<IActionResult> UpdatePrijavaStatus(int id, PrijavaStatusUpdateDto dto)
        {
            var prijava = await _context.Prijave
                .Include(p => p.Oglas)
                .Include(p => p.Korisnik)
                .FirstOrDefaultAsync(p => p.ID == id);

            if (prijava == null)
                return NotFound("Prijava ne postoji");

            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized("Korisnik nije autentifikovan");

            if (prijava.Oglas.PostavljacOglasaId != currentUserId)
                return Forbid("Samo vlasnik oglasa može da menja status prijave");
            if (prijava.Status == "Prihvacena")
                return BadRequest("Vec ste prihvatili prijavu");
            string stariStatus = prijava.Status;
            prijava.Status = dto.Status;

            try
            {
                await _context.SaveChangesAsync();

                if (dto.Status == "Prihvacena" && stariStatus != "Prihvacena")
                {
                    try
                    {
                        var chatService = HttpContext.RequestServices.GetRequiredService<IChatService>();
                        var chatDto = await chatService.CreateChatFromPrijavaAsync(id, currentUserId.Value);

                        var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();
                        await hubContext.Clients.Group($"user_{chatDto.KlijentId}").SendAsync("ChatCreated", chatDto);
                        await hubContext.Clients.Group($"user_{chatDto.OglasivacId}").SendAsync("ChatCreated", chatDto);

                        return Ok(new
                        {
                            Message = "Prijava prihvaćena i chat kreiran",
                            ChatId = chatDto.Id
                        });
                    }
                    catch (Exception ex)
                    {
                        var logger = HttpContext.RequestServices.GetRequiredService<ILogger<PrijaveController>>();
                        logger.LogError(ex, "Greška pri kreiranju chata za prijavu {PrijavaId}", id);

                        return Ok(new
                        {
                            Message = "Prijava prihvaćena, ali chat nije kreiran zbog greške",
                            Warning = ex.Message
                        });
                    }
                }

                return Ok(new { Message = $"Status prijave ažuriran na '{dto.Status}'" });
            }
            catch (Exception ex)
            {
                var logger = HttpContext.RequestServices.GetRequiredService<ILogger<PrijaveController>>();
                logger.LogError(ex, "Greška pri ažuriranju statusa prijave {PrijavaId}", id);
                return StatusCode(500, "Greška pri ažuriranju statusa prijave");
            }
        }


        // DELETE: api/Prijave/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePrijava(int id)
        {
            var prijava = await _context.Prijave.FindAsync(id);
            if (prijava == null)
            {
                return NotFound($"Prijava sa ID {id} nije pronađena.");
            }

            _context.Prijave.Remove(prijava);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool PrijavaExists(int id)
        {
            return _context.Prijave.Any(e => e.ID == id);
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
    }
}
