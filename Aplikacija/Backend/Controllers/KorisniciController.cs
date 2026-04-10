using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebTemplate.Services;
using WebTemplate.DTOs;

namespace WebTemplate.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class KorisniciController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthService _authService;

        public KorisniciController(ApplicationDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        // GET: api/Korisnici - Samo admin može videti sve korisnike
        [HttpGet]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<ActionResult<IEnumerable<UserInfoDto>>> GetKorisnici()
        {
            var korisnici = await _context.Korisnici
                .Select(k => new UserInfoDto
                {
                    Id = k.ID,
                    Ime = k.Ime,
                    Prezime = k.Prezime,
                    Username = k.Username,
                    Email = k.Email,
                    Role = k.Role,
                    CreatedAt = k.CreatedAt
                })
                .ToListAsync();

            return Ok(korisnici);
        }

        // GET: api/Korisnici/me - Trenutno prijavljeni korisnik
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UserInfoDto>> GetCurrentUser()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var korisnik = await _context.Korisnici
                .Where(k => k.ID == userId)
                .Select(k => new UserInfoDto
                {
                    Id = k.ID,
                    Ime = k.Ime,
                    Prezime = k.Prezime,
                    Username = k.Username,
                    Email = k.Email,
                    Role = k.Role,
                    CreatedAt = k.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (korisnik == null)
                return NotFound();

            return Ok(korisnik);
        }

        // GET: api/Korisnici/5 - Samo admin ili sam korisnik može videti svoj profil
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<UserInfoDto>> GetKorisnik(int id)
        {
            var currentUserId = GetCurrentUserId();
            var currentUserRole = GetCurrentUserRole();

            // Provera da li korisnik gleda svoj profil ili je admin
            if (currentUserId != id && currentUserRole != UserRoles.Admin)
                return Forbid();

            var korisnik = await _context.Korisnici
                .Where(k => k.ID == id)
                .Select(k => new UserInfoDto
                {
                    Id = k.ID,
                    Ime = k.Ime,
                    Prezime = k.Prezime,
                    Username = k.Username,
                    Email = k.Email,
                    Role = k.Role,
                    CreatedAt = k.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (korisnik == null)
                return NotFound();

            return Ok(korisnik);
        }

        [HttpGet("username/{username}")]
        [Authorize]
        public async Task<ActionResult<UserInfoDto>> GetKorisnikByUsername(string username)
        {
            var korisnik = await _context.Korisnici
                .Where(k => k.Username == username)
                .Select(k => new UserInfoDto
                {
                    Id = k.ID,
                    Ime = k.Ime,
                    Prezime = k.Prezime,
                    Username = k.Username,
                    Email = k.Email,
                    Biografija = k.Biografija,
                    Vestine = k.VestineJson,
                    Role = k.Role
                })
                .FirstOrDefaultAsync();

            if (korisnik == null)
                return NotFound();

            return Ok(korisnik);
        }

        // PUT: api/Korisnici/5 - Ažuriranje korisnika
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutKorisnik(int id, KorisnikUpdateDto korisnikDto)
        {
            var currentUserId = GetCurrentUserId();
            var currentUserRole = GetCurrentUserRole();

            // Provera da li korisnik menja svoj profil ili je admin
            if (currentUserId != id) //&& currentUserRole != UserRoles.Admin)
                return Forbid();
            if (currentUserRole != UserRoles.Admin)
                return Forbid();

            var korisnik = await _context.Korisnici.FindAsync(id);
            if (korisnik == null)
                return NotFound();

            if (!korisnikDto.Telefon.All(char.IsDigit))
                return BadRequest("Telefon se sastoji samo od cifara");
            // Ažuriranje polja
            if (!string.IsNullOrEmpty(korisnikDto.Ime))
                korisnik.Ime = korisnikDto.Ime;
            if (!string.IsNullOrEmpty(korisnikDto.Prezime))
                korisnik.Prezime = korisnikDto.Prezime;
            if (!string.IsNullOrEmpty(korisnikDto.Telefon))
                korisnik.Telefon = korisnikDto.Telefon;
            if (!string.IsNullOrEmpty(korisnikDto.Bio))
                korisnik.Biografija = korisnikDto.Bio;
            if (!string.IsNullOrEmpty(korisnikDto.Vestine))
                korisnik.VestineJson = korisnikDto.Vestine;

            if (!string.IsNullOrEmpty(korisnikDto.Email) && korisnikDto.Email != korisnik.Email)
            {
                // Provera da li email već postoji
                if (await _context.Korisnici.AnyAsync(u => u.Email == korisnikDto.Email && u.ID != id))
                    return BadRequest(new { message = "Email već postoji" });

                korisnik.Email = korisnikDto.Email;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!KorisnikExists(id))
                    return NotFound();
                throw;
            }
            var response = new LoginResponseDto
            {
                Token = "0",
                Expires = DateTime.UtcNow.AddHours(24),
                User = new UserInfoDto
                {
                    Id = korisnik.ID,
                    Ime = korisnik.Ime,
                    Prezime = korisnik.Prezime,
                    Username = korisnik.Username,
                    Biografija = korisnik.Biografija,
                    BrojTelefona = korisnik.Telefon,
                    SlikaUrl = korisnik.SlikaURL,
                    Email = korisnik.Email,
                    Role = korisnik.Role,
                    CreatedAt = korisnik.CreatedAt,
                    Vestine = korisnik.VestineJson
                }
            };
            return Ok(response);
        }

        // POST: api/Korisnici - Kreiranje korisnika (bez autorizacije - za registraciju)
        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult<UserInfoDto>> PostKorisnik(KorisnikCreateDto korisnikDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Provera da li username postoji
            if (await _context.Korisnici.AnyAsync(u => u.Username == korisnikDto.Username))
                return BadRequest(new { message = "Username već postoji" });

            // Provera da li email postoji
            if (await _context.Korisnici.AnyAsync(u => u.Email == korisnikDto.Email))
                return BadRequest(new { message = "Email već postoji" });

            bool isFirstUser = !await _context.Korisnici.AnyAsync();


            string hash = _authService.HashPassword(korisnikDto.Password);
            var korisnik = new Korisnik
            {
                Ime = korisnikDto.Ime,
                Prezime = korisnikDto.Prezime,
                Username = korisnikDto.Username,
                Email = korisnikDto.Email,
                PasswordHash = hash,
                Role = isFirstUser ? UserRoles.Admin : UserRoles.User,
                CreatedAt = DateTime.UtcNow,
                //dodati atributi, da ne bi pucalo
                Biografija="",
                SlikaURL="",
                Telefon=""
            };

            _context.Korisnici.Add(korisnik);
            await _context.SaveChangesAsync();

            var createdKorisnik = new UserInfoDto
            {
                Id = korisnik.ID,
                Ime = korisnik.Ime,
                Prezime = korisnik.Prezime,
                Username = korisnik.Username,
                Email = korisnik.Email,
                Role = korisnik.Role,
                CreatedAt = korisnik.CreatedAt
            };

            return CreatedAtAction("GetKorisnik", new { id = korisnik.ID }, createdKorisnik);
        }

        // DELETE: api/Korisnici/5
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteKorisnik(int id)
        {
            var currentUserId = GetCurrentUserId();
            var currentUserRole = GetCurrentUserRole();

            // Provera da li korisnik briše svoj profil ili je admin
            if (currentUserId != id && currentUserRole != UserRoles.Admin)
                return Forbid();

            var korisnik = await _context.Korisnici.FindAsync(id);
            if (korisnik == null)
                return NotFound();

            _context.Korisnici.Remove(korisnik);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        //// PUT: api/Korisnici/Ban/5
        //[Authorize(Roles = "Admin")]
        //[HttpPut("Ban/{id}")]
        //public async Task<IActionResult> BanUser(int id)
        //{
        //    var user = await _context.Korisnici
        //        .Include(u => u.Oglasi)
        //        .FirstOrDefaultAsync(u => u.ID == id);

        //    if (user == null)
        //        return NotFound();

        //    user.IsBanned = true;

        //    if (user.Oglasi != null)
        //        foreach (var oglas in user.Oglasi)
        //            oglas.IsActive = false;

        //    await _context.SaveChangesAsync();
        //    return Ok();
        //}

        // POST: api/Korisnici/profile-image
        [HttpPost("profile-image")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadProfileImage([FromForm] ProfileImageUploadDto dto)
        {
            var image = dto.Image;
            if (image == null || image.Length == 0)
                return BadRequest("Nema fajla");

            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId?.ToString()))
                return BadRequest("Ne mogu da nadjem ID korisnika");

            var folder = Path.Combine("wwwroot", "uploads", "avatars");
            Directory.CreateDirectory(folder);

            var fileName = $"user_{currentUserId}{Path.GetExtension(image.FileName)}";
            var path = Path.Combine(folder, fileName);

            using (var stream = new FileStream(path, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            var slikaUrl = $"/uploads/avatars/{fileName}";

            var korisnik = await _context.Korisnici.FindAsync(currentUserId);
            if (korisnik == null)
                return NotFound();

            korisnik.SlikaURL = slikaUrl;
            await _context.SaveChangesAsync();

            return Ok(new { slikaUrl });
        }

        // POST: api/Korisnici/PromoteToAdmin/33
        [HttpPut("PromoteToAdmin/{userId}")]
        [Authorize(Roles = "Admin")] // Samo admin može ovo da pozove
        public async Task<IActionResult> PromoteToAdmin(int userId)
        {
            var user = await _context.Korisnici.FindAsync(userId);
            if (user == null)
                return NotFound("Korisnik nije pronađen.");

            if (user.Role == "Admin")
                return BadRequest("Korisnik je već admin.");

            user.Role = "Admin";
            await _context.SaveChangesAsync();

            return Ok(new { userId = user.ID, newRole = user.Role });
        }

        [HttpPut("{id}/promeni-sifru")]
        public async Task<IActionResult> PromeniSifru(int id, PromenaSifreDto dto)
        {
            var user = await _context.Korisnici.FindAsync(id);
            if (user == null)
                return NotFound("Korisnik ne postoji");

            if (!_authService.VerifyPassword(dto.OldPassword,user.PasswordHash))
                return BadRequest(new { message = "Trenutna šifra nije ispravna" });
            if (dto.NewPassword.Length < 6)
                return BadRequest(new { message = "Prekratka sifra" });
            user.PasswordHash = _authService.HashPassword(dto.NewPassword);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Šifra uspešno promenjena" });
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
