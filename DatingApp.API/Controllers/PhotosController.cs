using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DatingApp.API.Data;
using DatingApp.API.Dtos;
using DatingApp.API.Helpers;
using DatingApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DatingApp.API.Controllers
{
    [Authorize]
    [Route("api/users/{userId}/photos")]
    [ApiController]
    public class PhotosController : ControllerBase
    {
        private readonly IDatingRepository _repo;
        private readonly IMapper _mapper;
        private readonly IOptions<CloudinarySettings> _cloudinaryConfig;
        private Cloudinary _cloudinary;

        public PhotosController(IDatingRepository repo,
         IMapper mapper,
         IOptions<CloudinarySettings> cloudinaryConfig)
        {
            _cloudinaryConfig = cloudinaryConfig;
            _mapper = mapper;
            _repo = repo;

            Account acc = new Account(
              _cloudinaryConfig.Value.CloudName,
              _cloudinaryConfig.Value.ApiKey,
              _cloudinaryConfig.Value.ApiSecret
            );

            _cloudinary = new Cloudinary(acc);
        }

        [HttpGet("{id}", Name = "GetPhoto")]
        public async Task<IActionResult> GetPhoto(int id)
        {
            var photoFromRepo = await _repo.GetPhoto(id);

            var photo = _mapper.Map<PhotoForReturnDto>(photoFromRepo);

            return Ok(photo);
        }

        [HttpPost]
        public async Task<IActionResult> AddPhotoForUser(int userId,
        [FromForm]PhotoForCreationDto photoForCreationDto)
        {
            // Checking if the correct user is trying to upload
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();

            // Getting user
            var userFromRepo = await _repo.GetUser(userId);
            // Creating a file from the Dto
            var file = photoForCreationDto.File;
            // Instantiation an ImageUploadResult
            var uploadResult = new ImageUploadResult();

            // Checking if the file is not empty
            if (file.Length > 0)
            {
                /* Using "using" so that the file which we are 
                 operating on will be dismissed once done operating  */
                using (var stream = file.OpenReadStream())
                {
                    var uploadParams = new ImageUploadParams()
                    {
                        // Using the DTO, we are instantiating the file's name from stream
                        File = new FileDescription(file.Name, stream),
                        // Cropping and resizing the photo on the face
                        Transformation = new Transformation()
                        .Width(500).Height(500)
                        .Crop("fill").Gravity("face")
                    };
                    // Uploading photo with details to Cloudinary
                    uploadResult = _cloudinary.Upload(uploadParams);
                }
            }
            // Setting the DTO properties to the results from cloudinary
            photoForCreationDto.Url = uploadResult.Uri.ToString();
            photoForCreationDto.PublicId = uploadResult.PublicId;

            // Mapping the photo object to the dto
            var photo = _mapper.Map<Photo>(photoForCreationDto);

            /* Checking if the user in the repo has any(using linq) photos, 
             If so, assign this photo to his main photo
             */
            if (!userFromRepo.Photos.Any(u => u.IsMain))
                photo.IsMain = true;
            // Add photo to user from repo
            userFromRepo.Photos.Add(photo);

            // Attempting save changes
            if (await _repo.SaveAll())
            {
                // Getting the mapping for photoforreturndto
                var photoToReturn = _mapper.Map<PhotoForReturnDto>(photo);
                // Call the specific Get response with a new Id object
                return CreatedAtRoute("GetPhoto", new { id = photo.Id }, photoToReturn);
            }
            return BadRequest("Couldn't add the photo");
        }

        [HttpPost("{id}/setMain")]
        public async Task<IActionResult> SetMainPhoto(int userId, int id)
        {
            // Checking if the correct user is trying to setmain
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();

            // Get the current user Id, and check if trying to set one of his photos to a main photo
            var user = await _repo.GetUser(userId);
            if (!user.Photos.Any(p => p.Id == id))
                return Unauthorized();

            // Get the MainPhoto from the repository, and check if it is already the main photo
            var photoFromRepo = await _repo.GetPhoto(id);
            if (photoFromRepo.IsMain)
                return BadRequest("This is already the main photo");

            // Get the current main photo, set it to false, set the new one (from repo) to true, and save
            var currentMainPhoto = await _repo.GetMainPhotoForUser(userId);
            currentMainPhoto.IsMain = false;
            photoFromRepo.IsMain = true;
            if (await _repo.SaveAll())
                return NoContent();

            // If reached here and couldn't save, return a bad request
            return BadRequest("Could not set photo to main");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePhoto(int userId, int id)
        {
            // Checking if the correct user is trying to delete a photo
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();

            // Get the current user Id, and check if trying to delete one of his photos
            var user = await _repo.GetUser(userId);
            if (!user.Photos.Any(p => p.Id == id))
                return Unauthorized();

            var photoFromRepo = await _repo.GetPhoto(id);
            if (photoFromRepo.IsMain)
                return BadRequest("You cannot delete your main photo");

            if (photoFromRepo.PublicId != null)
            {
                var deleteParams = new DeletionParams(photoFromRepo.PublicId);

                var result = _cloudinary.Destroy(deleteParams);
                // Check the result that we recieved from Cloudinary API, if OK, delete from repo
                if (result.Result == "ok")
                {
                    _repo.Delete(photoFromRepo);
                }
            }
            
            if (photoFromRepo.PublicId == null)
            {
                _repo.Delete(photoFromRepo);
            }

            // Check if was able to save and return OK
            if (await _repo.SaveAll())
                return Ok();

            return BadRequest("Failed to delete the photo");
        }

    }
}