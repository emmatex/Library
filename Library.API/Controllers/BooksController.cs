using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Library.API.Entities;
using Library.API.Models;
using Library.API.Services;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Library.API.Controllers
{
    [Route("api/authors/{authorId}/books")]
    [ApiController]
    public class BooksController : ControllerBase
    {
        private ILogger<BooksController> _logger;
        private IUrlHelper _urlHelper;
        private ILibraryRepository _libraryRepository;
        public BooksController(ILibraryRepository libraryRepository, ILogger<BooksController> logger,
            IUrlHelper urlHelper)
        {
            _logger = logger;
            _urlHelper = urlHelper;
            _libraryRepository = libraryRepository;
        }

        [HttpGet(Name = "GetBooksForAuthor")]
        public IActionResult GetBooksForAuthor(Guid authorId)
        {
            if (!_libraryRepository.AuthorExists(authorId)) return NotFound();

            var booksForAuthorFromRepo = _libraryRepository.GetBooksForAuthor(authorId);
            var booksForAuthor = Mapper.Map<IEnumerable<BookDto>>(booksForAuthorFromRepo);
            booksForAuthor = booksForAuthor.Select(book =>
            {
                book = CreateLinksForBook(book);
                return book;
            });

            var wrapper = new LinkedCollectionResourceWrapperDto<BookDto>(booksForAuthor);
            return Ok(CreateLinksForBooks(wrapper));
        }

        [HttpGet("{id}", Name = "GetBookForAuthor")]
        public IActionResult GetBookForAuthor(Guid authorId, Guid id)
        {
            if (!_libraryRepository.AuthorExists(authorId)) return NotFound();

            var bookForAuthorFromRepo = _libraryRepository.GetBookForAuthor(authorId, id);
            if(bookForAuthorFromRepo == null) return NotFound();

            var bookForAuthor = Mapper.Map<BookDto>(bookForAuthorFromRepo);
            return Ok(CreateLinksForBook(bookForAuthor));
        }

        [HttpPost(Name = "CreateBookForAuthor")]
        public IActionResult CreateBookForAuthor(Guid authorId, [FromBody]BookForCreateDto book)
        {
            if (book == null) return BadRequest();

            if (book.Title == book.Description)
                ModelState.AddModelError(nameof(BookForCreateDto), "The provided description should be different from title.");

            if (!ModelState.IsValid)
                return new UnprocessableEntityObjectResult(ModelState);

            if (!_libraryRepository.AuthorExists(authorId)) return NotFound();

            var bookToCreate = Mapper.Map<Book>(book);
            _libraryRepository.AddBookForAuthor(authorId, bookToCreate);

            if (!_libraryRepository.Save())
                throw new Exception($"Creating a book for author {authorId} failed on save.");
            
            var bookToReturn = Mapper.Map<BookDto>(bookToCreate);
            return CreatedAtRoute("GetBookForAuthor", new { id = bookToReturn.Id }, CreateLinksForBook(bookToReturn));
        }

        [HttpPut("{id}", Name = "UpdateBookForAuthor")]
        public IActionResult UpdateBookForAuthor(Guid authorId, Guid id, [FromBody]BookForUpdateDto book)
        {
            if (book == null) return BadRequest();

            if (book.Title == book.Description)
                ModelState.AddModelError(nameof(BookForUpdateDto), "The provided description should be different from title.");

            if (!ModelState.IsValid)
                return new UnprocessableEntityObjectResult(ModelState);

            if (!_libraryRepository.AuthorExists(authorId)) return NotFound();

            var bookForAuthorFromRepo = _libraryRepository.GetBookForAuthor(authorId, id);
            if (bookForAuthorFromRepo == null)
            {
                var bookToAdd = Mapper.Map<Book>(book);
                bookToAdd.Id = id;
                _libraryRepository.AddBookForAuthor(authorId, bookToAdd);

                if (!_libraryRepository.Save())
                    throw new Exception($"Upserting book {id} for author {authorId} failed on save.");

                var bookToReturn = Mapper.Map<BookDto>(bookToAdd);
                return CreatedAtRoute("GetBookForAuthor", new { id = bookToReturn.Id }, bookToReturn);
            }

            Mapper.Map(book, bookForAuthorFromRepo);
            _libraryRepository.UpdateBookForAuthor(bookForAuthorFromRepo);
            if (!_libraryRepository.Save())
                throw new Exception($"Updating book {id} for author {authorId} failed on update.");

            return NoContent();
        }

        [HttpPatch("{id}", Name = "PartiallyUpdateBookForAuthor")]
        public IActionResult PartiallyUpdateBookForAuthor(Guid authorId, Guid id, [FromBody] JsonPatchDocument<BookForUpdateDto> patchDoc)
        {
            if (patchDoc == null) return BadRequest();
            if (!_libraryRepository.AuthorExists(authorId)) return NotFound();
            var bookForAuthorFromRepo = _libraryRepository.GetBookForAuthor(authorId, id);
            if (bookForAuthorFromRepo == null)
            {
                var bookDto = new BookForUpdateDto();
                patchDoc.ApplyTo(bookDto, ModelState);

                if (bookDto.Title == bookDto.Description)
                    ModelState.AddModelError(nameof(BookForUpdateDto), "The provided description should be different from title.");

                TryValidateModel(bookDto);

                if (!ModelState.IsValid)
                    return new UnprocessableEntityObjectResult(ModelState);

                var bookToAdd = Mapper.Map<Book>(bookDto);
                bookToAdd.Id = id;
                _libraryRepository.AddBookForAuthor(authorId, bookToAdd);
                if (!_libraryRepository.Save())
                    throw new Exception($"Upserting book {id} for author {authorId} failed on save.");

                var bookToReturn = Mapper.Map<BookDto>(bookToAdd);
                return CreatedAtRoute("GetBookForAuthor", new { id = bookToReturn.Id }, bookToReturn);
            }

            var bookToPatch = Mapper.Map<BookForUpdateDto>(bookForAuthorFromRepo);
            patchDoc.ApplyTo(bookToPatch);
            if (bookToPatch.Title == bookToPatch.Description)
                ModelState.AddModelError(nameof(BookForUpdateDto), "The provided description should be different from title.");

            TryValidateModel(bookToPatch);

            if (!ModelState.IsValid)
                return new UnprocessableEntityObjectResult(ModelState);

            Mapper.Map(bookToPatch, bookForAuthorFromRepo);
            _libraryRepository.UpdateBookForAuthor(bookForAuthorFromRepo);
            if (!_libraryRepository.Save())
                throw new Exception($"Patching book {id} for author {authorId} failed on save.");

            return NoContent();
        }

        [HttpDelete("{id}", Name = "DeleteBookForAuthor")]
        public IActionResult DeleteBookForAuthor(Guid authorId, Guid id)
        {
            if (!_libraryRepository.AuthorExists(authorId)) return NotFound();
            var bookForAuthorFromRepo = _libraryRepository.GetBookForAuthor(authorId, id);
            if (bookForAuthorFromRepo == null) return NotFound();

            _libraryRepository.DeleteBook(bookForAuthorFromRepo);

            if (!_libraryRepository.Save())
                throw new Exception($"Deleting a book {id} for author {authorId} failed to delete.");

            _logger.LogInformation(100, $"Book {id} for author {authorId} was deleted.");

            return NoContent();
        }

        private BookDto CreateLinksForBook(BookDto book)
        {
            book.Links.Add(new LinkDto(_urlHelper.Link("GetBookForAuthor",
                new { id = book.Id }),
                "self",
                "GET"));

            book.Links.Add(
                new LinkDto(_urlHelper.Link("DeleteBookForAuthor",
                new { id = book.Id }),
                "delete_book",
                "DELETE"));

            book.Links.Add(
                new LinkDto(_urlHelper.Link("UpdateBookForAuthor",
                new { id = book.Id }),
                "update_book",
                "PUT"));

            book.Links.Add(
                new LinkDto(_urlHelper.Link("PartiallyUpdateBookForAuthor",
                new { id = book.Id }),
                "partially_update_book",
                "PATCH"));

            return book;
        }

        private LinkedCollectionResourceWrapperDto<BookDto> CreateLinksForBooks(
            LinkedCollectionResourceWrapperDto<BookDto> booksWrapper)
        {
            // link to self
            booksWrapper.Links.Add(
                new LinkDto(_urlHelper.Link("GetBooksForAuthor", new { }),
                "self",
                "GET"));

            return booksWrapper;
        }

    }
}