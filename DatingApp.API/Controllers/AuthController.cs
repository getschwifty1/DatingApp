using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using DatingApp.API.Data;
using DatingApp.API.Dtos;
using DatingApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace DatingApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthRepository _repo;
        private readonly IConfiguration _config;
        private readonly IMapper _mapper;
        public AuthController(IAuthRepository repo,
        IConfiguration config, IMapper mapper)
        {
            _mapper = mapper;
            _config = config;
            _repo = repo;
        }

    [HttpPost("register")]
    public async Task<IActionResult> Register(UserForRegisterDto userForRegisterDto)
    {

        userForRegisterDto.Username = userForRegisterDto.Username.ToLower();

        if (await _repo.UserExists(userForRegisterDto.Username))
            return BadRequest("Username already exists");

        var userToCreate = new User
        {
            UserName = userForRegisterDto.Username
        };

        var createdUser = await _repo.Register(userToCreate, userForRegisterDto.Password);

        // To Fix once we have a GetUser route
        return StatusCode(201);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(UserForLoginDto userForRegisterDto)
    {
        var userFromRepo = await _repo.Login(userForRegisterDto.Username.ToLower(), userForRegisterDto.Password);

        if (userFromRepo == null)
            return Unauthorized();

        // Token building process

        // Contains 2 claims made of userId and username
        var claims = new[]{
                new Claim(ClaimTypes.NameIdentifier, userFromRepo.Id.ToString()),
                new Claim(ClaimTypes.Name, userFromRepo.UserName)
            };

        // Creating security key. Takes from appsettings.json file, which we've added the Token part to.
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                _config.GetSection("AppSettings:Token").Value));

        // Hashing the key
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

        // Create the token descriptor where we pass the claims, expiry date (24 hrs), and the credentials
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.Now.AddDays(1),
            SigningCredentials = creds
        };

        // Creating a jwt security token handler object
        var tokenHandler = new JwtSecurityTokenHandler();

        // Store it in the token variable
        var token = tokenHandler.CreateToken(tokenDescriptor);

        var user = _mapper.Map<UserForListDto>(userFromRepo);
            // In the response, we send Ok + the new token as a new object
            return Ok(new
            {
                token = tokenHandler.WriteToken(token),
                user
            });

    }
}
}