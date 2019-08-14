using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Library.API.Entities;
using Library.API.Helpers;
using Library.API.Models;
using Library.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers
{
    [Route("api/authorcollections")]
    [ApiController]
    public class AuthorCollectionsController : ControllerBase
    {
        private readonly ILibraryRepository _libraryRepository;

        public AuthorCollectionsController(ILibraryRepository libraryRepository)
        {
            _libraryRepository = libraryRepository;
        }

        [HttpGet ("({ids})", Name = "GetAuthorCollection")]
        public IActionResult GetAuthorCollection([ModelBinder(BinderType = typeof(ArrayModelBinder))] IEnumerable<Guid> ids)
        {
            if (ids == null) return BadRequest();
            var authorCollectionFromRepo = _libraryRepository.GetAuthors(ids);
            // TODO : the ienumerable should only contain the specified ids, so that the check will work correctly
           // if (ids.Count() != authorCollectionFromRepo.Count()) return NotFound();
            var authorCollectionToReturn = Mapper.Map<IEnumerable<AuthorDto>>(authorCollectionFromRepo);
            return Ok(authorCollectionToReturn);
        }

        [HttpPost]
        public IActionResult CreateAuthorCollection([FromBody]IEnumerable<AuthorForCreateDto> authorCollection)
        {
            if (authorCollection == null) return BadRequest();
            var authorCollectionToCreate = Mapper.Map<IEnumerable<Author>>(authorCollection);
            foreach (var author in authorCollectionToCreate)
            {
                _libraryRepository.AddAuthor(author);
            }
            if (!_libraryRepository.Save())
                throw new Exception("Creating author collection fail to save.");

            var authorCollectionToReturn = Mapper.Map<IEnumerable<AuthorDto>>(authorCollectionToCreate);
            var idsAsString = string.Join(",", authorCollectionToReturn.Select(x => x.Id));
            return CreatedAtRoute("GetAuthorCollection", new {ids = idsAsString }, authorCollectionToReturn);
           
        }
    }
}