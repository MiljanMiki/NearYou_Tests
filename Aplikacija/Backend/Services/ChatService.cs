using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq.Expressions;
using WebTemplate.DTOs;
using WebTemplate.Hubs;

namespace WebTemplate.Services
{
    public interface IChatService
    {
        Task<ChatDto> CreateChatFromPrijavaAsync(int prijavaId, int oglasivacId);
        Task<List<ChatDto>> GetUserChatsAsync(int userId);
        Task<ChatPorukeDto> GetChatWithMessagesAsync(int chatId, int userId);
        Task<PorukaDto> SendMessageAsync(int chatId, int senderId, string message);
        Task<ChatDto> GetChatAsync(int chatId);

        Task<IActionResult> DeleteMessage(int userId, int chatId, int messageId);
        Task<PorukaDto> UpdateMessageAsync(int chatId, int messageId, string message);
    }

    public class ChatService : IChatService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            ApplicationDbContext context,
            IHubContext<ChatHub> hubContext,
            ILogger<ChatService> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<ChatDto> CreateChatFromPrijavaAsync(int prijavaId, int oglasivacId)
        {
            try
            {
                var prijava = await _context.Prijave
                    .Include(p => p.Oglas)
                    .Include(p => p.Korisnik)
                    .FirstOrDefaultAsync(p => p.ID == prijavaId && p.Status == "Prihvacena");

                if (prijava == null)
                    throw new InvalidOperationException("Prijava nije pronađena ili nije prihvaćena");

                if (prijava.Oglas.PostavljacOglasaId != oglasivacId)
                    throw new UnauthorizedAccessException("Samo vlasnik oglasa može kreirati chat");

                var existingChat = await _context.Chatovi
                    .FirstOrDefaultAsync(c => c.OglasId == prijava.OglasId && c.KlijentId == prijava.KorisnikId);

                if (existingChat != null)
                    throw new InvalidOperationException("Chat već postoji za ovu prijavu");

                var chat = new Chat
                {
                    OglasId = prijava.OglasId,
                    KlijentId = prijava.KorisnikId,
                    OglasivacId = prijava.Oglas.PostavljacOglasaId,
                    Kreiran = DateTime.UtcNow
                };

                _context.Chatovi.Add(chat);
                await _context.SaveChangesAsync();

                var oglasivac = await _context.Korisnici.FindAsync(chat.OglasivacId);

                var chatDto = new ChatDto
                {
                    Id = chat.Id,
                    OglasId = chat.OglasId,
                    OglasNaziv = prijava.Oglas.Naziv,
                    KlijentId = chat.KlijentId,
                    KlijentUsername = prijava.Korisnik.Username,
                    OglasivacId = chat.OglasivacId,
                    OglasivacUsername = oglasivac?.Username ?? string.Empty,
                    Kreiran = chat.Kreiran,
                    KlijentSlikaUrl = chat.Klijent.SlikaURL,
                    OglasivacSlikaUrl = chat.Oglasivac.SlikaURL
                };

                return chatDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri kreiranju chata iz prijave {PrijavaId}", prijavaId);
                throw;
            }
        }

        public async Task<List<ChatDto>> GetUserChatsAsync(int userId)
        {
            try
            {
                var chatovi = await _context.Chatovi
                    .Include(c => c.Oglas)
                    .Include(c => c.Klijent)
                    .Include(c => c.Oglasivac)
                    .Where(c => c.KlijentId == userId || c.OglasivacId == userId)
                    .OrderByDescending(c => c.PoslednjaPorukaVreme ?? c.Kreiran)
                    .Select(c => new ChatDto
                    {
                        Id = c.Id,
                        OglasId = c.OglasId,
                        OglasNaziv = c.Oglas.Naziv,
                        KlijentId = c.KlijentId,
                        KlijentUsername = c.Klijent.Username,
                        OglasivacId = c.OglasivacId,
                        OglasivacUsername = c.Oglasivac.Username,
                        Kreiran = c.Kreiran,
                        PoslednjaPoruka = c.PoslednjaPoruka,
                        PoslednjaPorukaVreme = c.PoslednjaPorukaVreme,
                        PoslednjaPorukaPosiljalac = c.PoslednjaPorukaPosiljalac,
                        KlijentSlikaUrl = c.Klijent.SlikaURL,
                        OglasivacSlikaUrl = c.Oglasivac.SlikaURL
                    })
                    .ToListAsync();

                return chatovi;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri učitavanju chatova za korisnika {UserId}", userId);
                throw;
            }
        }

        public async Task<ChatPorukeDto> GetChatWithMessagesAsync(int chatId, int userId)
        {
            try
            {
                var chat = await _context.Chatovi
                    .Include(c => c.Oglas)
                    .Include(c => c.Klijent)
                    .Include(c => c.Oglasivac)
                    .Include(c => c.Poruke)
                        .ThenInclude(p => p.Posiljalac)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null)
                    throw new KeyNotFoundException($"Chat sa ID {chatId} nije pronađen");

                // Provera da li korisnik učestvuje u chatu
                if (chat.KlijentId != userId && chat.OglasivacId != userId)
                    throw new UnauthorizedAccessException("Nemate pristup ovom chatu");

                var chatDto = new ChatDto
                {
                    Id = chat.Id,
                    OglasId = chat.OglasId,
                    OglasNaziv = chat.Oglas.Naziv,
                    KlijentId = chat.KlijentId,
                    KlijentUsername = chat.Klijent.Username,
                    OglasivacId = chat.OglasivacId,
                    OglasivacUsername = chat.Oglasivac.Username,
                    Kreiran = chat.Kreiran,
                    PoslednjaPoruka = chat.PoslednjaPoruka,
                    PoslednjaPorukaVreme = chat.PoslednjaPorukaVreme,
                    PoslednjaPorukaPosiljalac = chat.PoslednjaPorukaPosiljalac,
                    KlijentSlikaUrl = chat.Klijent.SlikaURL,
                    OglasivacSlikaUrl = chat.Oglasivac.SlikaURL
                };

                var poruke = chat.Poruke
                    .OrderBy(p => p.VremeSlanja)
                    .Select(p => new PorukaDto
                    {
                        Id = p.Id,
                        ChatId = p.ChatId,
                        PosiljalacId = p.PosiljalacId,
                        PosiljalacUsername = p.Posiljalac.Username,
                        Tekst = p.Tekst,
                        VremeSlanja = p.VremeSlanja
                    })
                    .ToList();

                return new ChatPorukeDto
                {
                    Chat = chatDto,
                    Poruke = poruke
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri učitavanju poruka za chat {ChatId}", chatId);
                throw;
            }
        }

        public async Task<PorukaDto> SendMessageAsync(int chatId, int senderId, string message)
        {
            try
            {
                var chat = await _context.Chatovi
                    .Include(c => c.Klijent)
                    .Include(c => c.Oglasivac)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null)
                    throw new KeyNotFoundException($"Chat sa ID {chatId} nije pronađen");

                if (chat.KlijentId != senderId && chat.OglasivacId != senderId)
                    throw new UnauthorizedAccessException("Nemate pristup ovom chatu");

                var poruka = new Poruka
                {
                    ChatId = chatId,
                    PosiljalacId = senderId,
                    Tekst = message,
                    VremeSlanja = DateTime.UtcNow
                };

                _context.Poruke.Add(poruka);

                chat.PoslednjaPorukaVreme = poruka.VremeSlanja;
                chat.PoslednjaPoruka = message;

                var posiljalac = await _context.Korisnici.FindAsync(senderId);
                chat.PoslednjaPorukaPosiljalac = posiljalac?.Username;

                await _context.SaveChangesAsync();

                var porukaDto = new PorukaDto
                {
                    Id = poruka.Id,
                    ChatId = poruka.ChatId,
                    PosiljalacId = poruka.PosiljalacId,
                    PosiljalacUsername = posiljalac?.Username ?? string.Empty,
                    Tekst = poruka.Tekst,
                    VremeSlanja = poruka.VremeSlanja
                };

                await NotifyChatParticipantsAsync(chatId, porukaDto);

                return porukaDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri slanju poruke u chat {ChatId}", chatId);
                throw;
            }
        }

        public async Task<ChatDto> GetChatAsync(int chatId)
        {
            try
            {
                var chat = await _context.Chatovi
                    .Include(c => c.Oglas)
                    .Include(c => c.Klijent)
                    .Include(c => c.Oglasivac)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null)
                    throw new KeyNotFoundException($"Chat sa ID {chatId} nije pronađen");

                return new ChatDto
                {
                    Id = chat.Id,
                    OglasId = chat.OglasId,
                    OglasNaziv = chat.Oglas.Naziv,
                    KlijentId = chat.KlijentId,
                    KlijentUsername = chat.Klijent.Username,
                    OglasivacId = chat.OglasivacId,
                    OglasivacUsername = chat.Oglasivac.Username,
                    Kreiran = chat.Kreiran,
                    PoslednjaPoruka = chat.PoslednjaPoruka,
                    PoslednjaPorukaVreme = chat.PoslednjaPorukaVreme,
                    PoslednjaPorukaPosiljalac = chat.PoslednjaPorukaPosiljalac,
                    KlijentSlikaUrl = chat.Klijent.SlikaURL,
                    OglasivacSlikaUrl = chat.Oglasivac.SlikaURL
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri dobavljanju chata {ChatId}", chatId);
                throw;
            }
        }

        private async Task NotifyChatParticipantsAsync(int chatId, PorukaDto porukaDto)
        {
            try
            {
                var chat = await _context.Chatovi.FindAsync(chatId);
                if (chat == null) return;

                await _hubContext.Clients.Group($"user_{chat.KlijentId}").SendAsync("ReceiveMessage", porukaDto);
                await _hubContext.Clients.Group($"user_{chat.OglasivacId}").SendAsync("ReceiveMessage", porukaDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri slanju SignalR notifikacije za chat {ChatId}", chatId);
            }
        }

        public async Task<IActionResult> DeleteMessage(int userId,int chatId, int messageId)
        {

            var chat = await _context.Chatovi.FindAsync(chatId);
            if (chat == null)
                throw new KeyNotFoundException($"Chat sa ID {chatId} nije pronađen");
            //ili samo ovo?
            //var poruka = await _context.Poruke.FirstOrDefaultAsync(m => m.Id == messageId);
            
            var poruka = chat.Poruke.FirstOrDefault(m => m.Id == messageId);
            if (poruka == null)
                throw new KeyNotFoundException($"Poruka sa ID {messageId} ne postoji!");

            if (userId != poruka.PosiljalacId)
                throw new ArgumentException($"ID posiljaoca:{poruka.PosiljalacId} i ID korisnika:{userId} koji brise poruku nisu isti!");

            try
            {
                _context.Poruke.Remove(poruka);
                await _context.SaveChangesAsync();
            }
            catch(Exception e)
            {
                _logger.LogError(e, "Greska prilikom brisanja poruke iz base.");
                throw;
            }

            return new NoContentResult();
        }
        public async Task<PorukaDto> UpdateMessageAsync(int chatId, int messageId, string message)
        {
            var chat = await _context.Chatovi.FindAsync(chatId);
            if (chat == null)
                throw new KeyNotFoundException($"Chat sa ID {chatId} nije pronađen");

            //ili samo ovo?
            //var poruka = await _context.Poruke.FirstOrDefaultAsync(m => m.Id == messageId);

            var poruka = chat.Poruke.FirstOrDefault(m => m.Id == messageId);
            if (poruka == null)
                throw new KeyNotFoundException($"Poruka sa ID {messageId} ne postoji!");


            try
            {
                await _context.SaveChangesAsync();

            }
            catch(DbUpdateConcurrencyException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Greska prilikom azuriranja teksta poruka za chatID: {chatId} i msgId:{messageId}");
                throw;
            }

            var response= new PorukaDto();
            return response;
        }

    }
}