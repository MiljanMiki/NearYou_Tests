using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Security.Claims;
using WebTemplate.DTOs;
using WebTemplate.Services;

namespace WebTemplate.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IChatService chatService, ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        [HttpGet("my")]
        [Authorize]
        public async Task<ActionResult<List<ChatDto>>> GetMyChats()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            try
            {
                var chatovi = await _chatService.GetUserChatsAsync(userId.Value);
                return Ok(chatovi);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri dobavljanju chatova za korisnika {UserId}", userId);
                return StatusCode(500, "Greška pri dobavljanju chatova");
            }
        }

        [HttpGet("{id}/messages")]
        [Authorize]
        public async Task<ActionResult<ChatPorukeDto>> GetChatMessages(int id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            try
            {
                var chatPoruke = await _chatService.GetChatWithMessagesAsync(id, userId.Value);
                return Ok(chatPoruke);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri dobavljanju poruka za chat {ChatId}", id);
                return StatusCode(500, "Greška pri dobavljanju poruka");
            }
        }

        [HttpPost("create-from-prijava")]
        [Authorize]
        public async Task<ActionResult<ChatDto>> CreateChatFromPrijava([FromBody] KreirajChatDto dto)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            try
            {
                var chatDto = await _chatService.CreateChatFromPrijavaAsync(dto.PrijavaId, userId.Value);
                return CreatedAtAction(nameof(GetChatMessages), new { id = chatDto.Id }, chatDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri kreiranju chata iz prijave {PrijavaId}", dto.PrijavaId);
                return StatusCode(500, "Greška pri kreiranju chata");
            }
        }

        [HttpPost("{chatId}/message")]
        [Authorize]
        public async Task<ActionResult<PorukaDto>> SendMessage(int chatId, [FromBody] SendMessageDto dto)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            try
            {
                var porukaDto = await _chatService.SendMessageAsync(chatId, userId.Value, dto.Message);
                return Ok(porukaDto);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri slanju poruke u chat {ChatId}", chatId);
                return StatusCode(500, "Greška pri slanju poruke");
            }
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<ChatDto>> GetChat(int id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            try
            {
                var chat = await _chatService.GetChatAsync(id);
                return Ok(chat);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri dobavljanju chata {ChatId}", id);
                return StatusCode(500, "Greška pri dobavljanju chata");
            }
        }

        //[HttpPut("{chatID}/{messageID")]
        //[Authorize]
        //public async Task<IActionResult> PutMessage(int chatID, int messageID,[FromBody] UpdateMessageDto dto)
        //{
        //    var userId = GetCurrentUserId();
        //    if (!userId.HasValue)
        //        return Unauthorized();

        //   try
        //    {
        //        var response = await _chatService.UpdateMessageAsync(chatID, messageID, dto.NewMessageContent);
        //        return Ok(response);
        //    }
        //    catch(KeyNotFoundException e)
        //    {

        //    }
        //    catch(Exception e)
        //    {

        //    }

        //}

        [HttpDelete("{chatID}/{messageID}")]
        [Authorize]
        public async Task<IActionResult> DeleteMessage(int chatID, int messageID)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();


            try
            {
                await _chatService.DeleteMessage((int)userId, chatID, messageID);
            }
            catch(KeyNotFoundException e)
            {
                return NotFound(e.Message);
            }
            catch(ArgumentException e)
            {
                return NotFound(e.Message);
            }
            catch(Exception e)
            {
                return BadRequest(e.Message);
            }
            return Ok($"Uspesno izbrisana poruka sa ID-jem {messageID} iz baze.");
        }
        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }

    public class SendMessageDto
    {
        [Required]
        [MaxLength(1000)]
        public string Message { get; set; }
    }

    public class UpdateMessageDto
    {
        [Required]
        [MaxLength(1000)]
        public string NewMessageContent { get; set; }
    }


}