using AutoMapper;
using MyWebApi.Data;
using MyWebApi.Models;

namespace MyWebApi.Helpers
{
    public class ApplicationMapper : Profile
    {
        public ApplicationMapper()
        {
            CreateMap<Book, BookModel>().ReverseMap();
        }
    }
}
