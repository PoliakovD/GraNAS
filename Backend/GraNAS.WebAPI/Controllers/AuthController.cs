using System;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GraNAS.Models;
using GraNAS.Models.DTO;
using GraNAS.WebAPI.DAL.Repositories.Interfaces;
using GraNAS.WebAPI.Services.Interfaces;

namespace GraNAS.WebAPI.Controllers;

 [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;

        public AuthController(
            IUserRepository userRepository,
            IPasswordHasher passwordHasher,
            ITokenService tokenService)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // 1. Базовая валидация модели (атрибуты)
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // 2. Дополнительная валидация сложности пароля (можно расширить)
            if (!IsPasswordStrong(request.Password))
            {
                ModelState.AddModelError("Password",
                    "Password must contain at least one uppercase letter, one lowercase letter, and one digit.");
                return BadRequest(ModelState);
            }

            // 3. Проверка уникальности email
            var emailExists = await _userRepository.EmailExistsAsync(request.Email);
            if (emailExists)
            {
                return Conflict(new { error = "email_already_exists",
                    error_description = "User with this email already exists." });
            }

            // 4. Хеширование пароля
            var passwordHash = _passwordHasher.HashPassword(request.Password);

            // 5. Создание пользователя
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                PasswordHash = passwordHash,
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow
            };

            await _userRepository.CreateAsync(user);

            // 6. Возвращаем успешный ответ (без токенов, только подтверждение регистрации)
            var response = new RegisterResponse
            {
                UserId = user.Id,
                Message = "Registration successful."
            };

            return Ok(response);
        }

        private bool IsPasswordStrong(string password)
        {
            // Минимальные требования: длина >= 6, хотя бы одна заглавная, одна строчная, одна цифра
            if (password.Length < 6)
                return false;

            bool hasUpper = Regex.IsMatch(password, "[A-Z]");
            bool hasLower = Regex.IsMatch(password, "[a-z]");
            bool hasDigit = Regex.IsMatch(password, "[0-9]");

            return hasUpper && hasLower && hasDigit;
        }
    }
